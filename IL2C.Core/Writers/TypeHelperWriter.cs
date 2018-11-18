﻿using System.Linq;

using IL2C.Metadata;

namespace IL2C.Writers
{
    internal static class TypeHelperWriter
    {
        //private struct VirtualFunctionInformation
        //{
        //    public readonly string MangledTypeName;
        //    public readonly string CLanguageTypeName;
        //    public readonly string CLanguageVirtualFunctionDeclarationName;
        //    public readonly string CLanguageReturnTypeName;
        //    public readonly KeyValuePair<string, string>[] Parameters;

        //    public VirtualFunctionInformation(
        //        string mangledTypeName,
        //        string cLanguageTypeName,
        //        string cLanguageVirtualFunctionDeclarationName,
        //        string cLanguageReturnTypeName,
        //        KeyValuePair<string, string>[] parameters)
        //    {
        //        this.MangledTypeName = mangledTypeName;
        //        this.CLanguageTypeName = cLanguageTypeName;
        //        this.CLanguageVirtualFunctionDeclarationName = cLanguageVirtualFunctionDeclarationName;
        //        this.CLanguageReturnTypeName = cLanguageReturnTypeName;
        //        this.Parameters = parameters;
        //    }
        //}

        //private static VirtualFunctionInformation[] GetVirtualFunctions(
        //    ITypeInformation adjustorThunkTargetType,
        //    ITypeInformation delegationTargetType) =>
        //    adjustorThunkTargetType
        //        .DeclaredMethods.Where(m => m.IsVirtual)        // TODO: virtual method
        //        .Select(method => new VirtualFunctionInformation(
        //            method.DeclaringType.Equals(adjustorThunkTargetType)
        //                ? delegationTargetType.MangledName
        //                : method.DeclaringType.MangledName,
        //            method.DeclaringType.Equals(adjustorThunkTargetType)
        //                ? delegationTargetType.CLanguageTypeName
        //                : method.DeclaringType.CLanguageTypeName,
        //            method.CLanguageVirtualFunctionDeclarationName,
        //            method.ReturnType.CLanguageTypeName,
        //            method.GetParameters(adjustorThunkTargetType)
        //                .Select((parameter, index) => Utilities.KeyValue(
        //                    (index == 0) ? parameter.TargetType.CLanguageThisTypeName : parameter.TargetType.CLanguageTypeName,
        //                    parameter.SymbolName))
        //                .ToArray()))
        //        .ToArray();

        public static void InternalConvertTypeHelper(
            CodeTextWriter tw,
            ITypeInformation declaredType)
        {
            tw.WriteLine("//////////////////////");
            tw.WriteLine("// [7] Runtime helpers:");

            // Enum is same as primitive numeric value type.
            // Delegate mark handler is same as System.MulticastDelegate.
            if (!declaredType.IsEnum && !declaredType.IsDelegate)
            {
                // Write mark handler:
                tw.SplitLine();
                tw.WriteLine(
                    "// [7-5] GC's mark handler");
                tw.WriteLine(
                    "void __{0}_IL2C_MarkHandler__({1} this__)",
                    declaredType.MangledName,
                    declaredType.CLanguageThisTypeName);
                tw.WriteLine("{");

                using (var _ = tw.Shift())
                {
                    tw.WriteLine(
                        "il2c_assert(this__ != NULL);");

                    var fields = declaredType.Fields.
                        Where(field => !field.IsStatic && field.FieldType.IsReferenceType).
                        ToArray();
                    if (fields.Length >= 1)
                    {
                        tw.SplitLine();
                        tw.WriteLine(
                            "// [7-6] Try marking each object reference fields");

                        // Write unbox function if type is value type.
                        string thisRefForMarker;
                        if (declaredType.IsValueType)
                        {
                            tw.WriteLine(
                                "{0}* pValue =",
                                declaredType.CLanguageTypeName);

                            using (var __ = tw.Shift())
                            {
                                tw.WriteLine(
                                    "il2c_unsafe_unbox__(this__, {0});",
                                    declaredType.CLanguageTypeName);
                            }

                            thisRefForMarker = "pValue";
                        }
                        else
                        {
                            thisRefForMarker = "this__";
                        }

                        // Write marker expression.
                        foreach (var field in fields)
                        {
                            tw.WriteLine(
                                "il2c_try_mark_from_handler({0}->{1});",
                                thisRefForMarker,
                                field.Name);
                        }
                    }

                    // NOTE:
                    //   Invoke base class mark handler except contains NO fields.
                    //   (ex: System.Object, System.ValueType...)
                    var baseType = declaredType.BaseType;
                    if (baseType != null)
                    {
                        if (baseType.
                            Traverse(type => type.BaseType).
                            Any(type => type.Fields.Length >= 1) == false)
                        {
                            tw.SplitLine();
                            tw.WriteLine(
                                "// [7-7] Delegate checking base types");
                            tw.WriteLine(
                                "__{0}_IL2C_MarkHandler__(({1})this__);",
                                declaredType.BaseType.MangledName,
                                declaredType.BaseType.CLanguageTypeName);
                        }
                        else
                        {
                            tw.SplitLine();
                            tw.WriteLine(
                                "/* Suppressed invoke base mark handler */");
                        }
                    }
                }

                tw.WriteLine("}");
                tw.SplitLine();
            }

            // Write trampoline virtual functions if type is value type.
            var virtualMethods = declaredType.CalculatedVirtualMethods;
            var trampolineTargets = virtualMethods.
                Where(entry => declaredType.IsValueType && entry.method.DeclaringType.Equals(declaredType)).
                Select(entry => entry.method).
                ToArray();
            foreach (var method in trampolineTargets)
            {
                tw.WriteLine(
                    "// [7-12] Trampoline virtual function: {0}",
                    method.FriendlyName);
                tw.WriteLine(
                    "static {0} {1}_Trampoline_VFunc__(System_ValueType* this__{2})",
                    method.ReturnType.CLanguageTypeName,
                    method.CLanguageFunctionName,
                    string.Concat(method.Parameters.
                        Skip(1).
                        Select(p => string.Format(", {0} {1}", p.TargetType.CLanguageTypeName, p.ParameterName))));
                tw.WriteLine(
                    "{");

                using (var _ = tw.Shift())
                {
                    tw.WriteLine(
                        "il2c_assert(this__ != NULL);");
                    tw.SplitLine();
                    tw.WriteLine(
                        "{0}* pValue =",
                        declaredType.CLanguageTypeName);

                    using (var __ = tw.Shift())
                    {
                        tw.WriteLine(
                            "il2c_unsafe_unbox__(this__, {0});",
                            declaredType.CLanguageTypeName);
                    }

                    tw.WriteLine(
                        "return {0}(pValue{1});",
                        method.CLanguageFunctionName,
                        string.Concat(method.Parameters.
                            Skip(1).
                            Select(p => string.Format(", {0}", p.ParameterName))));    // These aren't required expression evaluation.
                }

                tw.WriteLine(
                    "}");
                tw.SplitLine();
            }

            var overrideMethods = declaredType.OverrideMethods;
            var newSlotMethods = declaredType.NewSlotMethods;
            var overrideBaseMethods = declaredType.OverrideBaseMethods;

#if true
            // If virtual method collection doesn't contain reuseslot and newslot method at declared types:
            if (!overrideMethods.Any() && !newSlotMethods.Any(method => method.DeclaringType.Equals(declaredType)))
            {
                tw.WriteLine(
                    "// [7-10-1] VTable (Not defined, same as {0})",
                    declaredType.BaseType.FriendlyName);
                tw.SplitLine();
            }
            // Require new vtable.
            else
            {
                // Write virtual methods
                tw.WriteLine(
                    "// [7-10-2] VTable");
                tw.WriteLine(
                    "{0}_VTABLE_DECL__ {0}_VTABLE__ = {{",
                    declaredType.MangledName);

                using (var _ = tw.Shift())
                {
                    foreach (var (method, _) in virtualMethods)
                    {
                        // MEMO: Transfer trampoline virtual function if declared type is value type.
                        //   Because arg0 type is native value type pointer, but the virtual function requires boxed objref.
                        //   The trampoline unboxes from objref to target value type.
                        tw.WriteLine(
                            "({0}){1}{2},",
                            method.CLanguageFunctionTypePrototype,
                            method.CLanguageFunctionName,
                            (declaredType.IsValueType && method.DeclaringType.Equals(declaredType)) ? "_Trampoline_VFunc__" : string.Empty);
                    }
                }

                tw.WriteLine("};");
                tw.SplitLine();
            }
#else
            // Write virtual methods
            tw.WriteLine(
                "// [7-10] Vtable of {0}",
                declaredType.FriendlyName);
            tw.WriteLine(
                "{0}_VTABLE_DECL__ {0}_VTABLE__ = {{",
                declaredType.MangledName);

            var virtualFunctions = GetVirtualFunctions(
                declaredType,
                declaredType);

            foreach (var function in virtualFunctions)
            {
                tw.WriteLine(
                    "{0}{1}_{2},",
                    indent,
                    function.MangledTypeName,
                    function.CLanguageVirtualFunctionDeclarationName);
            }

            tw.WriteLine("};");
            tw.SplitLine();

            foreach (var interfaceType in declaredType.InterfaceTypes)
            {
                var interfaceVirtualFunctions = GetVirtualFunctions(
                    interfaceType,
                    declaredType);

                foreach (var function in interfaceVirtualFunctions)
                {
                    // Adjustor thunk will not invoke direct, so try to emit static function.
                    tw.WriteLine(
                        "// [7-11] Adjustor thunk: {0}.{1}",
                        function.CLanguageTypeName,
                        function.CLanguageVirtualFunctionDeclarationName);
                    tw.WriteLine(
                        "static {0} __{1}_{2}_AT_{3}__(",
                        function.CLanguageReturnTypeName,
                        function.CLanguageTypeName,
                        function.CLanguageVirtualFunctionDeclarationName,
                        interfaceType.MangledName);
                    tw.WriteLine(
                        "{0}{1})",
                        indent,
                        string.Join(
                            ", ",
                            function.Parameters.Select(parameter =>
                                string.Format(
                                    "{0} {1}",
                                    parameter.Key,
                                    parameter.Value))));
                    tw.WriteLine(
                        "{");
                    tw.WriteLine(
                        "{0}{1}__{2}_{3}__({4});",
                        indent,
                        (function.CLanguageReturnTypeName != "void") ? "return " : string.Empty,
                        function.CLanguageTypeName,
                        function.CLanguageVirtualFunctionDeclarationName,
                        string.Join(
                            ", ",
                            function.Parameters.Select((parameter, index) =>
                                (index == 0)
                                    // Adjust vptr offset with il2c_cast_from_interface() macro.
                                    ? string.Format(
                                        "({0}*)il2c_cast_from_interface({1}, {2}, {3})",
                                        function.CLanguageTypeName,
                                        declaredType.MangledName,
                                        interfaceType.MangledName,
                                        parameter.Value)
                                    : parameter.Value)));
                    tw.WriteLine(
                        "}");
                    tw.SplitLine();
                }

                tw.WriteLine(
                    "// [7-12] Vtable of {0} (with adjustor thunk)",
                    interfaceType.FriendlyName);
                tw.WriteLine(
                    "{0}_VTABLE_DECL__ {1}_{0}_VTABLE__ = {{",
                    interfaceType.MangledName,
                    declaredType.MangledName);

                foreach (var function in interfaceVirtualFunctions)
                {
                    tw.WriteLine(
                        "{0}__{1}_{2}_AT_{3}__,",
                        indent,
                        function.CLanguageTypeName,
                        function.CLanguageVirtualFunctionDeclarationName,
                        interfaceType.MangledName);
                }

                tw.WriteLine("};");
                tw.SplitLine();
            }
#endif

            // Write runtime type information
            tw.WriteLine("// [7-8] Runtime type information");

            // IL2C_RUNTIME_TYPE_BEGIN(System_ValueType, "System.ValueType", IL2C_TYPE_REFERENCE, System_Object, 0, 0)
            tw.WriteLine(
                "IL2C_RUNTIME_TYPE_BEGIN({0}, \"{1}\", {2}, {3}, {4}, {5})",
                declaredType.MangledName,
                declaredType.FriendlyName, // Type name (UTF-8 string, C compiler embeds)
                declaredType.IsEnum ?      // Type attribute flags
                    (declaredType.ElementType.IsUnsigned ? "IL2C_TYPE_UNSIGNED_INTEGER" : "IL2C_TYPE_INTEGER") :
                    declaredType.IsDelegate ? "IL2C_TYPE_VARIABLE" :
                    declaredType.IsInterface ? "IL2C_TYPE_INTERFACE" :
                    declaredType.IsReferenceType ? "IL2C_TYPE_REFERENCE" :
                    "IL2C_TYPE_VALUE",
                    declaredType.BaseType.MangledName,
                    declaredType.IsDelegate ? "System_Delegate_MarkHandler__" : "0",
                    0);

            // TODO: mark target offsets
            // TODO: interfaces

            tw.WriteLine(
                "IL2C_RUNTIME_TYPE_END();");
            tw.SplitLine();
        }

        public static void InternalConvertTypeHelperForInterface(
            CodeTextWriter tw,
            ITypeInformation declaredType)
        {
            tw.WriteLine("//////////////////////");
            tw.WriteLine("// [8] Runtime helpers:");

            // Write runtime type information
            tw.SplitLine();
            tw.WriteLine("// [8-1] Runtime type information");

            // IL2C_RUNTIME_TYPE_BEGIN(System_ValueType, "System.ValueType", IL2C_TYPE_REFERENCE, System_Object, 0, 0)
            tw.WriteLine(
                "IL2C_RUNTIME_TYPE_BEGIN({0}, \"{1}\", {2}, {3}, {4}, {5})",
                declaredType.MangledName,
                declaredType.FriendlyName, // Type name (UTF-8 string, C compiler embeds)
                declaredType.IsEnum ?      // Type attribute flags
                    (declaredType.ElementType.IsUnsigned ? "IL2C_TYPE_UNSIGNED_INTEGER" : "IL2C_TYPE_INTEGER") :
                    declaredType.IsDelegate ? "IL2C_TYPE_VARIABLE" :
                    declaredType.IsInterface ? "IL2C_TYPE_INTERFACE" :
                    declaredType.IsReferenceType ? "IL2C_TYPE_REFERENCE" :
                    "IL2C_TYPE_VALUE",
                    declaredType.BaseType.MangledName,
                    declaredType.IsDelegate ? "System_Delegate_MarkHandler__" : "0",
                    0);

            // TODO: interfaces

            tw.WriteLine(
                "IL2C_RUNTIME_TYPE_END();");
            tw.SplitLine();
        }
    }
}
