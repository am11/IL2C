﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;

using IL2C.Metadata.Attributes;

namespace IL2C.Metadata
{
    public interface IMethodInformation : IMemberInformation
    {
        bool IsPublic { get; }
        bool IsPrivate { get; }
        bool IsFamily { get; }
        bool IsFamilyOrAssembly { get; }
        bool IsFamilyAndAssembly { get; }

        bool IsConstructor { get; }
        bool IsStatic { get; }
        bool IsVirtual { get; }
        bool IsOverridedOrImplemented { get; }
        bool IsAbstract { get; }
        bool IsSealed { get; }
        bool IsNewSlot { get; }
        bool IsReuseSlot { get; }
        bool IsExtern { get; }
        bool HasThis { get; }
        bool HasBody { get; }

        ITypeInformation ReturnType { get; }
        IParameterInformation[] Parameters { get; }
        ILocalVariableInformation[] LocalVariables { get; }
        IMethodInformation[] Overrides { get; }

        (ITypeInformation, IMethodInformation)? GetOverridedOrImplementedMethod();

        ICodeStream CodeStream { get; }
        int OverloadIndex { get; }

        IParameterInformation[] GetParameters(ITypeInformation thisType);

        PInvokeInfo PInvokeInformation { get; }
        NativeMethodAttributeInformation NativeMethod { get; }

        string CLanguageFunctionName { get; }
        string CLanguageFunctionDeclarationName { get; }
        string CLanguageFunctionPrototype { get; }
        string CLanguageFunctionType { get; }
        string CLanguageFunctionTypePrototype { get; }
    }

    internal sealed class MethodInformation
        : MemberInformation<MethodReference, MethodDefinition>, IMethodInformation
    {
        private static readonly DebugInformation[] emptyDebug = new DebugInformation[0];
        private static readonly CustomAttribute[] emptyCustomAttribute = new CustomAttribute[0];

        public MethodInformation(MethodReference method, ModuleInformation module)
            : base(method, module)
        {
        }

        public override string MemberTypeName =>
            this.IsConstructor ?
                (this.IsStatic ? "Type initializer" : "Constructor") :
                this.IsStatic ?
                    "Static method" :
                    this.IsAbstract ?
                        this.DeclaringType.IsInterface ?
                            "Interface method" :
                            "Abstract method" :
                        this.IsVirtual ?
                            "Virtual method" :
                            "Method";

        public override string AttributeDescription
        {
            get
            {
                var scope = this.IsPublic ?
                    "public" :
                    this.IsFamily ?
                    "protected" :
                    this.IsFamilyOrAssembly ?
                    "protected internal" :
                    this.IsFamilyAndAssembly ?
                    "private protected" :
                    this.IsPrivate ?
                    "private" :
                    "internal";
                var attribute1 = this.IsStatic ?
                    "static" :
                    this.IsVirtual ?
                    (this.IsReuseSlot ? "override" : "virtual") :
                    string.Empty;
                var attribute2 = this.IsSealed ?
                    "sealed" :
                    this.IsExtern ?
                    "extern" :
                    string.Empty;

                return string.Join(" ",
                    new[] { scope, attribute1, attribute2 }.
                    Where(a => !string.IsNullOrWhiteSpace(a)));
            }
        }

        private IParameterInformation CreateThisParameterInformation(ITypeInformation thisType) =>
            new ParameterInformation(
                this,
                0,
                "this__",
                thisType.IsValueType ? thisType.MakeByReference() : thisType,
                emptyCustomAttribute);

        private IParameterInformation ToParameterInformation(ParameterReference parameter) =>
            new ParameterInformation(
                this,
                this.HasThis ? (parameter.Index + 1) : parameter.Index,
                parameter.Name,
                this.MetadataContext.GetOrAddType(parameter.ParameterType),
                parameter.Resolve().CustomAttributes.ToArray());

        public override string MetadataTypeName => "Method";

        public bool IsPublic =>
            this.Definition.IsPublic;
        public bool IsPrivate =>
            this.Definition.IsPrivate;
        public bool IsFamily =>
            this.Definition.IsFamily;
        public bool IsFamilyOrAssembly =>
            this.Definition.IsFamilyOrAssembly;
        public bool IsFamilyAndAssembly =>
            this.Definition.IsFamilyAndAssembly;

        public bool IsConstructor =>
            this.Definition.IsConstructor;
        public bool IsStatic =>
            this.Definition.IsStatic;
        public bool IsVirtual =>
            this.Definition.IsVirtual &&
            // HACK:
            //   1. We can drop newslot virtual if the class is sealed. (ex: the "Invoke" method at derived delegate type generated by the Roslyn.)
            //   2. The method at value type maybe marked virtual, so dropped it to make excluding from vtable entry.
            !(this.IsSealed && this.IsNewSlot) &&
            !(this.DeclaringType.IsValueType && !this.IsReuseSlot);

        public (ITypeInformation, IMethodInformation)? GetOverridedOrImplementedMethod() =>
            this.DeclaringType?.InterfaceTypes.
                Select(t => (t, t.DeclaredMethods.FirstOrDefault(m => MetadataUtilities.VirtualMethodSignatureComparer.Equals(m, this)))).
                FirstOrDefault(entry => entry.Item2 != null);

        public bool IsOverridedOrImplemented =>
            this.Definition.IsVirtual || this.GetOverridedOrImplementedMethod().HasValue;
        public bool IsAbstract =>
            this.Definition.IsAbstract;
        public bool IsSealed =>
            this.Definition.IsFinal || this.DeclaringType.IsSealed;
        public bool IsNewSlot =>
            this.Definition.IsNewSlot;
        public bool IsReuseSlot =>
            this.Definition.IsReuseSlot;
        public bool IsExtern =>
            this.Definition.IsPInvokeImpl || this.Definition.IsInternalCall ||
            // HACK: Marked extern but doesn't have the IL body (will produced by Roslyn)
            // HACK: Externed but not marked method (ex: delegate constructor)
            (!this.HasBody && (this.IsConstructor || !this.IsAbstract));
        public bool HasThis =>
            this.Definition.HasThis;
        public bool HasBody => 
            this.Definition.HasBody && (this.Definition.Body?.CodeSize >= 1);

        public ITypeInformation ReturnType =>
            this.MetadataContext.GetOrAddType(this.Member.ReturnType) ?? this.MetadataContext.VoidType;
        public IParameterInformation[] Parameters =>
            (this.Member.HasThis) ?
                new[] { this.CreateThisParameterInformation(this.DeclaringType) }.
                    Concat(this.Member.Parameters.Select(this.ToParameterInformation)).
                    ToArray() :
                this.Member.Parameters.
                    Select(ToParameterInformation).
                    ToArray();
        public ILocalVariableInformation[] LocalVariables =>
            this.Definition.Body.Variables.
                Select(variable => new LocalVariableInformation(
                    this,
                    variable.Index,
                    this.Definition.Body.Method.DebugInformation.TryGetName(variable, out var name) ?       // TODO: every failed?
                        name :
                        string.Format("local{0}__", variable.Index),
                    this.MetadataContext.GetOrAddType(variable.VariableType))).
                ToArray();
        public IMethodInformation[] Overrides =>
            this.Definition.Overrides.
            Select(om => this.MetadataContext.GetOrAddMethod(om)).
            ToArray();

        private static int GetExceptionHandlerTypePriority(ExceptionHandlerType type)
        {
            switch (type)
            {
                case ExceptionHandlerType.Catch:
                case ExceptionHandlerType.Filter:
                case ExceptionHandlerType.Fault: return 0;
                case ExceptionHandlerType.Finally: return 1;
                default:
                    throw new Exception();
            }
        }

        public ICodeStream CodeStream
        {
            get
            {
                if (!this.HasBody)
                {
                    return null;
                }

                var body = this.Definition.Body;
                Debug.Assert(body != null);

                // It gathers sequence point informations.
                // It will use writing the line preprocessor directive.
                var paths = new Dictionary<string, string>();
                var spd =
                    (from sp in this.Definition.DebugInformation.SequencePoints
                     where !sp.IsHidden
                     group sp by sp.Offset into g
                     let sps = g.
                        OrderBy(sp => sp.Offset).
                        Select(sp => new DebugInformation(
                            paths.GetOrAdd(sp.Document.Url, sp.Document.Url),
                            sp.StartLine,
                            sp.StartColumn)).
                        ToArray()
                     where sps.Length >= 1
                     select new { g.Key, sps }).
                    ToDictionary(g => g.Key, g => g.sps);

                var exceptionHandlers =
                    body.ExceptionHandlers.
                    GroupBy(eh => (tryStart: eh.TryStart.Offset, tryEnd: eh.TryEnd.Offset)).
                    OrderBy(g => g.Key.tryStart).
                    ThenByDescending(g => g.Key.tryEnd).
                    Select(g => new ExceptionHandler(
                        g.Key.tryStart, g.Key.tryEnd,
                        // Ordered by handler type: catch --> finally
                        g.OrderBy(eh => GetExceptionHandlerTypePriority(eh.HandlerType)).
                            Select(eh => new ExceptionCatchHandler(
                                (eh.HandlerType == ExceptionHandlerType.Catch) ? ExceptionCatchHandlerTypes.Catch : ExceptionCatchHandlerTypes.Finally,
                                this.MetadataContext.GetOrAddType(eh.CatchType),
                                eh.HandlerStart.Offset,
                                // HACK: The handler end offset sometimes doesn't produce at the last sequence.
                                eh.HandlerEnd?.Offset ??
                                    (body.Instructions.Last().Offset +
                                    body.Instructions.Last().OpCode.Size))).
                        ToArray())).
                    ToArray();

                var codeStream = new CodeStream(exceptionHandlers);

                object translateOperand(object operand)
                {
                    var inst = operand as Instruction;
                    if (inst != null)
                    {
                        return codeStream[inst.Offset];
                    }

                    var parameter = operand as ParameterReference;
                    if (parameter != null)
                    {
                        return this.Parameters[this.HasThis ? (parameter.Index + 1) : parameter.Index];
                    }

                    var local = operand as VariableReference;
                    if (local != null)
                    {
                        return this.LocalVariables[local.Index];
                    }

                    var typeRef = operand as TypeReference;
                    if (typeRef != null)
                    {
                        return this.MetadataContext.GetOrAddType(typeRef);
                    }

                    var fieldRef = operand as FieldReference;
                    if (fieldRef != null)
                    {
                        return this.MetadataContext.GetOrAddField(fieldRef);
                    }

                    var methodRef = operand as MethodReference;
                    if (methodRef != null)
                    {
                        return this.MetadataContext.GetOrAddMethod(methodRef);
                    }

                    return operand;
                }

                foreach (var inst in body.Instructions.
                    OrderBy(instruction => instruction.Offset).
                    Select(instruction => new CodeInformation(
                        this,
                        instruction.Offset,
                        instruction.OpCode,
                        instruction.Operand,
                        instruction.GetSize(),
                        spd.TryGetValue(instruction.Offset, out var sps) ? sps : emptyDebug,
                        translateOperand)))
                {
                    codeStream.Add(inst.Offset, inst);
                }
                return codeStream;
            }
        }

        public int OverloadIndex
        {
            get
            {
                var dict = this.DeclaringType.DeclaredMethods.
                    CalculateOverloadMethods();
                var found = dict[this.Member.Name].
                    Select((m, i) => new { m, i }).
                    First(e => this.Equals(e.m));
                return found.i;
            }
        }

        public IParameterInformation[] GetParameters(ITypeInformation thisType)
        {
            Debug.Assert(this.Member.HasThis);

            return new[] { this.CreateThisParameterInformation(thisType) }.
                Concat(this.Member.Parameters.Select(this.ToParameterInformation)).
                ToArray();
        }

        public PInvokeInfo PInvokeInformation =>
            this.Definition.PInvokeInfo;
        public NativeMethodAttributeInformation NativeMethod =>
            this.Definition.CustomAttributes.
            Where(ca => ca.AttributeType.FullName == NativeMethodAttributeInformation.FullName).
            Select(ca => new NativeMethodAttributeInformation(ca)).
            FirstOrDefault();

        private ITypeInformation GetInterfaceImplementerType() =>
            this.GetOverridedOrImplementedMethod()?.Item1;

        public override MemberScopes CLanguageMemberScope
        {
            get
            {
                var scope = MemberScopes.HiddenOrUnknown;
                var definition = this.Definition;

                if (definition.IsPrivate)
                {
                    scope = MemberScopes.File;
                }
                else if (definition.IsFamily || definition.IsFamilyAndAssembly)
                {
                    scope = MemberScopes.Linkage;
                }
                else if (definition.IsPublic || definition.IsFamilyOrAssembly)
                {
                    scope = MemberScopes.Public;
                }

                var declaringType = this.DeclaringType;
                if (declaringType.CLanguageMemberScope < scope)
                {
                    scope = declaringType.CLanguageMemberScope;
                }

                // The virtual method or interface implemented method expose wider scope than member declaration.
                // Because these methods are required the vtable entries from the derived class types.
                if (declaringType.IsClass)
                {
                    if (definition.IsVirtual &&
                        (declaringType.CLanguageMemberScope > scope))
                    {
                        scope = declaringType.CLanguageMemberScope;
                    }
                    var interfaceType = this.GetInterfaceImplementerType();
                    if (interfaceType?.CLanguageMemberScope > scope)
                    {
                        scope = interfaceType.CLanguageMemberScope;
                    }
                }

                return scope;
            }
        }

        public string CLanguageFunctionName =>
            // System_Int32 Foo.Bar.Baz.MooMethod(System.String name) --> Foo_Bar_Baz_MooMethod__System_String
            this.MangledUniqueName;

        public string CLanguageFunctionDeclarationName =>
            // System_Int32 Foo.Bar.Baz.MooMethod(System.String) --> MooMethod__System_String   (Will use vptr name)
            this.Member.GetMangledUniqueName(true);

        private string GetCLanguageFunctionType(bool isPrototype)
        {
            // The first argument (arg0) is switched type name by virtual attribute between strict type and "void*."
            //   non virtual : int32_t (*DoThat)(System_String* this__)
            //   virtual     : int32_t (*DoThat)(void* this__)

            var parametersString = (this.Parameters.Length >= 1) ?
                string.Join(
                    ", ",
                    this.Parameters.Select((parameter, index) =>
                        ((this.IsVirtual && (index == 0)) ? "void*" : parameter.TargetType.CLanguageTypeName) +
                        (isPrototype ? (" " + parameter.ParameterName) : string.Empty))) :
                "void";

            var returnTypeName = this.ReturnType.CLanguageTypeName;

            return string.Format(
                "{0} (*{1})({2})",
                returnTypeName,
                isPrototype ? this.CLanguageFunctionDeclarationName : string.Empty,
                parametersString);
        }

        public string CLanguageFunctionType =>
            // System_Int32 Foo.Bar.Baz.MooMethod(System.String) --> int32_t (*)(System_String*)
            this.GetCLanguageFunctionType(false);

        public string CLanguageFunctionTypePrototype =>
            // System_Int32 Foo.Bar.Baz.MooMethod(System.String) --> int32_t (*MooMethod__System_String)(System_String* this__)
            this.GetCLanguageFunctionType(true);

        public string CLanguageFunctionPrototype
        {
            // System_Int32 Foo.Bar.Baz.MooMethod(System.String name) --> int32_t Foo_Bar_Baz_MooMethod__System_String(System_String* name)
            get
            {
                var parametersString = (this.Parameters.Length >= 1) ?
                    string.Join(
                        ", ",
                        this.Parameters.Select(parameter => string.Format(
                            "{0} {1}",
                            parameter.TargetType.CLanguageTypeName,
                            parameter.ParameterName))) :
                    "void";

                var returnTypeName =
                    this.ReturnType.CLanguageTypeName;

                return string.Format(
                    "{0} {1}({2})",
                    returnTypeName,
                    this.CLanguageFunctionName,
                    parametersString);
            }
        }

        protected override MethodDefinition OnResolve(MethodReference member) =>
            member.Resolve();
    }
}
