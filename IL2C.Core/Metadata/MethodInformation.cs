﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;

namespace IL2C.Metadata
{
    [Flags]
    public enum FriendlyNameTypes
    {
        Less = 0x00,
        FullName = 0x01,
        ArgumentTypes = 0x02,
        ArgumentNames = 0x03,
        Index = 0x08,
        Full = 0x0f,
        Mangled = 0x10,
    }

    public interface IMethodInformation : IMemberInformation
    {
        bool IsPublic { get; }
        bool IsFamily { get; }
        bool IsFamilyOrAssembly { get; }

        bool IsConstructor { get; }
        bool IsStatic { get; }
        bool IsVirtual { get; }
        bool IsAbstract { get; }
        bool IsSealed { get; }
        bool IsNewSlot { get; }
        bool IsReuseSlot { get; }
        bool IsExtern { get; }
        bool HasThis { get; }
        bool HasBody { get; }

        ITypeInformation ReturnType { get; }
        VariableInformation[] Parameters { get; }
        VariableInformation[] LocalVariables { get; }
        IMethodInformation[] Overrides { get; }
        IMethodInformation BaseMethod { get; }

        ICodeStream CodeStream { get; }
        int OverloadIndex { get; }

        string GetFriendlyName(FriendlyNameTypes type = FriendlyNameTypes.Full);
        VariableInformation[] GetParameters(ITypeInformation thisType);

        PInvokeInfo PInvokeInfo { get; }

        string CLanguageFunctionName { get; }
        string CLanguageFunctionPrototype { get; }
        string CLanguageFunctionTypePrototype { get; }

        string GetCLanguageDeclarationName(int overloadIndex);
        string GetCLanguageFunctionPrototype(int overloadIndex);
    }

    internal sealed class MethodInformation
        : MemberInformation<MethodReference, MethodDefinition>, IMethodInformation
    {
        private static readonly DebugInformation[] empty = new DebugInformation[0];

        public MethodInformation(MethodReference method, ModuleInformation module)
            : base(method, module)
        {
        }

        public override string MemberTypeName =>
            this.IsStatic
                ? "Static method"
                : this.IsAbstract
                    ? this.DeclaringType.IsInterface
                        ? "Interface method"
                        : "Abstract method"
                    : "Method";

        private VariableInformation CreateThisParameterInformation(ITypeInformation thisType) =>
            new VariableInformation(
                this,
                0,
                "this__",
                thisType.IsValueType ? thisType.MakeByReference() : thisType);

        private VariableInformation ToParameterInformation(ParameterReference parameter) =>
            new VariableInformation(
                this,
                this.HasThis ? (parameter.Index + 1) : parameter.Index,
                parameter.Name,
                this.MetadataContext.GetOrAddMember(
                    parameter.ParameterType,
                    type_ => new TypeInformation(
                        type_,
                        this.MetadataContext.GetOrAddModule(
                            type_.Module.Assembly,
                            type_.Module,
                            (assembly_, module_) => new ModuleInformation(
                                module_,
                                this.MetadataContext.GetOrAddAssembly(
                                    assembly_,
                                    assembly__ => new AssemblyInformation(assembly__, this.MetadataContext)))))));

        public override string MetadataTypeName => "Method";

        public override string FriendlyName =>
            this.GetFriendlyName(FriendlyNameTypes.ArgumentTypes | FriendlyNameTypes.ArgumentNames);

        public override string MangledName =>
            this.GetFriendlyName(FriendlyNameTypes.Index | FriendlyNameTypes.Mangled);

        public bool IsPublic =>
            this.Definition.IsPublic;
        public bool IsFamily =>
            this.Definition.IsFamily;
        public bool IsFamilyOrAssembly =>
            this.Definition.IsFamilyOrAssembly;

        public bool IsConstructor =>
            this.Definition.IsConstructor;
        public bool IsStatic =>
            this.Definition.IsStatic;
        public bool IsVirtual =>
            this.Definition.IsVirtual &&
            // HACK: The method at value type maybe marked virtual, so it made unmarking at this expression.
            !(this.DeclaringType.IsValueType && !this.IsReuseSlot);
        public bool IsAbstract =>
            this.Definition.IsAbstract;
        public bool IsSealed =>
            this.Definition.IsFinal || this.DeclaringType.IsSealed;
        public bool IsNewSlot =>
            this.Definition.IsNewSlot;
        public bool IsReuseSlot =>
            this.Definition.IsReuseSlot;
        public bool IsExtern =>
            this.Definition.IsPInvokeImpl || this.Definition.IsInternalCall;
        public bool HasThis =>
            this.Definition.HasThis;
        public bool HasBody => 
            this.Definition.HasBody;

        public ITypeInformation ReturnType =>
            this.MetadataContext.GetOrAddMember(
                this.Member.ReturnType,
                type => (type != null) ? new TypeInformation(type, this.DeclaringModule) : this.MetadataContext.VoidType);
        public VariableInformation[] Parameters =>
            (this.Member.HasThis) ?
                new[] { this.CreateThisParameterInformation(this.DeclaringType) }.
                    Concat(this.Member.Parameters.Select(this.ToParameterInformation)).
                    ToArray() :
                this.Member.Parameters.
                    Select(ToParameterInformation).
                    ToArray();
        public VariableInformation[] LocalVariables =>
            this.Definition.Body.Variables.
                Select(variable => new VariableInformation(
                    this,
                    variable.Index,
                    this.Definition.Body.Method.DebugInformation.TryGetName(variable, out var name) ?
                        name :
                        string.Format("local{0}__", variable.Index),
                    this.MetadataContext.GetOrAddMember(
                        variable.VariableType,
                        variableType => new TypeInformation(
                            variableType,
                            this.MetadataContext.GetOrAddModule(
                                variableType.Module.Assembly,
                                variableType.Module,
                                (assembly, module_) => new ModuleInformation(
                                    module_,
                                    this.MetadataContext.GetOrAddAssembly(
                                        assembly,
                                        assembly_ => new AssemblyInformation(assembly_, this.MetadataContext)))))))).
                ToArray();
        public IMethodInformation[] Overrides =>
            this.Definition.Overrides.
                Select(om => this.MetadataContext.GetOrAddMember(
                    om, m => new MethodInformation(m, this.DeclaringModule))).
                ToArray();
        public IMethodInformation BaseMethod =>
            this.DeclaringType.BaseType?.
                Traverse(type => type.BaseType).
                SelectMany(type => type.DeclaredMethods.Where(m => m.Overrides.Contains(this))).
                FirstOrDefault();

        public ICodeStream CodeStream
        {
            get
            {
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

                var codeStream = new CodeStream();

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
                        return this.MetadataContext.GetOrAddMember(
                            typeRef, t => new TypeInformation(t, this.DeclaringModule));
                    }

                    var fieldRef = operand as FieldReference;
                    if (fieldRef != null)
                    {
                        return this.MetadataContext.GetOrAddMember(
                            fieldRef, f => new FieldInformation(f, this.DeclaringModule));
                    }

                    var methodRef = operand as MethodReference;
                    if (methodRef != null)
                    {
                        return this.MetadataContext.GetOrAddMember(
                            methodRef, m => new MethodInformation(m, this.DeclaringModule));
                    }

                    return operand;
                }

                foreach (var inst in this.Definition.Body.Instructions.
                    OrderBy(instruction => instruction.Offset).
                    Select(instruction => new CodeInformation(
                        this,
                        instruction.Offset,
                        instruction.OpCode,
                        instruction.Operand,
                        instruction.GetSize(),
                        spd.TryGetValue(instruction.Offset, out var sps) ? sps : empty,
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

        private static bool FullName(FriendlyNameTypes type) =>
            (type & FriendlyNameTypes.FullName) == FriendlyNameTypes.FullName;

        private static bool IncludeNames(FriendlyNameTypes type) =>
            (type & FriendlyNameTypes.ArgumentNames) == FriendlyNameTypes.ArgumentNames;

        private static bool IncludeTypes(FriendlyNameTypes type) =>
            (type & FriendlyNameTypes.ArgumentTypes) == FriendlyNameTypes.ArgumentTypes;

        private static bool IncludeIndex(FriendlyNameTypes type) =>
            (type & FriendlyNameTypes.Index) == FriendlyNameTypes.Index;

        private static bool Mangled(FriendlyNameTypes type) =>
            (type & FriendlyNameTypes.Mangled) == FriendlyNameTypes.Mangled;

        public string GetFriendlyName(FriendlyNameTypes type = FriendlyNameTypes.Full)
        {
            // Apply index number if NOT default method (method have no arguments)
            var index = (IncludeIndex(type) && (this.OverloadIndex >= 1))
                ? string.Format("@{0}", this.OverloadIndex)
                : string.Empty;

            var arguments = (IncludeNames(type) || IncludeTypes(type))
                ? string.Format(
                    "({0})",
                    string.Join(
                        ", ",
                        this.Parameters.Select(parameter =>
                            (IncludeNames(type) && IncludeTypes(type))
                                ? string.Format(
                                    "{0} {1}",
                                    parameter.TargetType.FriendlyName,
                                    parameter.SymbolName)
                                : IncludeTypes(type)
                                    ? parameter.TargetType.FriendlyName
                                    : parameter.SymbolName)))
                : string.Empty;

            var name = string.Format(
                "{0}{1}{2}",
                FullName(type) ? this.Member.GetFriendlyName() : this.Member.Name,
                index,
                arguments);

            return Mangled(type) ? Utilities.ToMangledName(name) : name;
        }

        public VariableInformation[] GetParameters(ITypeInformation thisType)
        {
            Debug.Assert(this.Member.HasThis);

            return new[] { this.CreateThisParameterInformation(thisType) }
                .Concat(this.Member.Parameters.Select(this.ToParameterInformation))
                .ToArray();
        }

        public PInvokeInfo PInvokeInfo =>
            this.Definition.PInvokeInfo;

        public override bool IsCLanguagePublicScope =>
            this.Definition.IsPublic;
        public override bool IsCLanguageLinkageScope =>
            !this.Definition.IsPublic && !this.Definition.IsPrivate;
        public override bool IsCLanguageFileScope =>
            this.Definition.IsPrivate;

        public string CLanguageFunctionName =>
            this.GetFriendlyName(FriendlyNameTypes.FullName | FriendlyNameTypes.Index | FriendlyNameTypes.Mangled);

        public string CLanguageFunctionPrototype
        {
            get
            {
                var parametersString = string.Join(
                    ", ",
                    this.Parameters.Select(parameter => string.Format(
                        "{0} {1}",
                        parameter.TargetType.CLanguageTypeName,
                        parameter.SymbolName)));

                var returnTypeName =
                    this.ReturnType.CLanguageTypeName;

                return string.Format(
                    "{0} {1}({2})",
                    returnTypeName,
                    this.CLanguageFunctionName,
                    parametersString);
            }
        }

        public string CLanguageFunctionTypePrototype =>
            this.GetCLanguageFunctionPrototype(-1);

        public string GetCLanguageDeclarationName(int overloadIndex)
        {
            return
                (overloadIndex == 0) ? this.Name :
                (overloadIndex == -1) ? string.Empty :
                string.Format("{0}_{1}", this.Name, overloadIndex);
        }

        public string GetCLanguageFunctionPrototype(int overloadIndex)
        {
            // Generate function type prototype if overloadIndex == -1.
            //   [0] : int32_t (*GetHashCode)(void* this__)
            //   [1] : int32_t (*GetHashCode_1)(void* this__)
            //   [2] : int32_t (*GetHashCode_2)(void* this__)
            //   [-1]: int32_t (*)(void*)

            // The first argument (arg0) is switched type name by virtual attribute between strict type and "void*."
            //   non virtual : int32_t (*DoThat)(System_String* this__)
            //   virtual     : int32_t (*DoThat)(void* this__)

            var parametersString = string.Join(
                ", ",
                this.Parameters.Select((parameter, index) => string.Format(
                    "{0}{1}",
                    (this.IsVirtual && (index == 0)) ? "void*" : parameter.TargetType.CLanguageTypeName,
                    (overloadIndex == -1) ? string.Empty : (" " + parameter.SymbolName))));

            var returnTypeName = this.ReturnType.CLanguageTypeName;
            var name = this.GetCLanguageDeclarationName(overloadIndex);

            return string.Format(
                "{0} (*{1})({2})",
                returnTypeName,
                name,
                parametersString);
        }

        protected override MethodDefinition OnResolve(MethodReference member) =>
            member.Resolve();
    }
}
