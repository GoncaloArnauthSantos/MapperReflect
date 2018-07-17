using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Text;

namespace MapperReflect
{

    public abstract class AssemblyCreator
    {
        public AssemblyName aName;
        public AssemblyBuilder ab;
        public ModuleBuilder mb;
        public TypeBuilder tb;

        /// <summary>
        /// Creates a new instance of <c>AssemblyCreator</c> creating an new assembly
        /// on current AppDomain with the given <paramref name="aName"/>, initializes 
        /// a new modle inside the newly created Assembly and defines a new type of 
        /// <paramref name="typeToCreate"/>.
        /// </summary>
        /// <param name="aName">the assembly name</param>
        /// <param name="typeToCreate">the Type of the new Type to define</param>
        public AssemblyCreator(AssemblyName aName, Type typeToCreate)
        {
            this.aName = aName;
            ab = AppDomain.CurrentDomain.DefineDynamicAssembly(
                aName,
                AssemblyBuilderAccess.RunAndSave);
            mb = ab.DefineDynamicModule(
                aName.Name, aName.Name + ".dll");
            createType(typeToCreate);
        }

        /// <summary>
        /// creates the new Type of <paramref name="typeToCreate"/> inside the newly
        /// created module
        /// </summary>
        /// <param name="typeToCreate"></param>
        private void createType(Type typeToCreate)
        {
            tb = mb.DefineType(
                aName.Name + typeToCreate.Name,
                TypeAttributes.Public,
                typeToCreate);
        }

        public static void generateCastAndUnboxCode(ILGenerator ilGenerator, Type castType)
        {
            ilGenerator.Emit(OpCodes.Castclass, castType);
            if (castType.IsValueType)
            {
                ilGenerator.Emit(OpCodes.Unbox, castType);
            }
        }

        public static void generateCastAndUnboxAnyCode(ILGenerator ilGenerator, Type castType)
        {
            LocalBuilder local = ilGenerator.DeclareLocal(typeof(Object));
            var toZero = ilGenerator.DefineLabel();
            var toEnd = ilGenerator.DefineLabel();
            ilGenerator.Emit(OpCodes.Stloc, local);
            ilGenerator.Emit(OpCodes.Ldloc, local);
            ilGenerator.Emit(OpCodes.Castclass, castType);
            if (castType.IsPrimitive)
            {
                ilGenerator.Emit(OpCodes.Brfalse, toZero);
                ilGenerator.Emit(OpCodes.Ldloc, local);
                ilGenerator.Emit(OpCodes.Unbox_Any, castType);
                ilGenerator.Emit(OpCodes.Br, toEnd);
                ilGenerator.MarkLabel(toZero);
                ilGenerator.Emit(OpCodes.Ldc_I4_0);
                ilGenerator.MarkLabel(toEnd);
            }
            else if (castType.IsValueType)
            {
                ilGenerator.Emit(OpCodes.Unbox_Any, castType);
            }
        }

        public static void generateBoxCode(ILGenerator ilGenerator, Type castType)
        {
            if (castType.IsValueType)
            {
                ilGenerator.Emit(OpCodes.Box, castType);
            }
        }
    }

    public interface IMemberMapping
    {
        object Map(object src);
        void Match(string nameFrom, string nameTo);
        void For<R>(string nameTo, Func<R> func);
    }

    public abstract class MemberMapping : IMemberMapping
    {
        internal Type KlassSrc { get; }
        internal Type KlassDst { get; }
        internal bool IsToMapFields { get; }
        internal bool IsToMapProperties { get; }
        internal Type AttributeTypeToMap { get; }
        internal List<IGetterAndSetter> GettersAndSetters { get; }

        internal MemberMapping(Type klassSrc, Type klassDst, bool isToMapFields, bool isToMapProperties, Type attributeTypeToMap)
        {
            KlassSrc = klassSrc;
            KlassDst = klassDst;
            IsToMapFields = isToMapFields;
            IsToMapProperties = isToMapProperties;
            AttributeTypeToMap = attributeTypeToMap;
            GettersAndSetters = new List<IGetterAndSetter>();
        }

        internal bool HasAttributeToMap(MemberInfo miSrc)
        {
            return AttributeTypeToMap == null || miSrc.GetCustomAttribute(AttributeTypeToMap, false) != null;
        }

        internal IMemberGetter CreateGetter(MemberInfo miSrc, bool ignoreMap = false)
        {
            if (ignoreMap || HasAttributeToMap(miSrc))
            {
                if (miSrc.MemberType == MemberTypes.Property && (ignoreMap || IsToMapProperties))
                    return MemberGetterCreator.For((PropertyInfo)miSrc);
                else if (miSrc.MemberType == MemberTypes.Field && (ignoreMap || IsToMapFields))
                    return MemberGetterCreator.For((FieldInfo)miSrc);
            }
            return null;
        }

        internal Boolean TryMapping(MemberInfo miSrc, MemberInfo[] miDsts, bool ignoreMap = false, bool useAutoMapper = false)
        {
            IMemberGetter getter = CreateGetter(miSrc, ignoreMap);
            if (getter == null) return false;
            return TryMapping( getter, miDsts, ignoreMap, useAutoMapper);
        }

        internal Boolean TryMapping(IMemberGetter getter, MemberInfo[] miDsts, bool ignoreMap = false, bool useAutoMapper = false)
        {
            foreach (MemberInfo miDst in miDsts)
            {
                IMemberSetter setter = MemberSetterCreator.For(miDst);
                if (setter == null) continue;
                if (setter.SetterType.IsAssignableFrom(getter.GetterType))
                    GettersAndSetters.Add(new GetterAndSetter(getter, setter));
                else if (useAutoMapper)
                    GettersAndSetters.Add(new MapperGetterAndSetter(getter, setter, AutoMapper.Build(getter.GetterType, setter.SetterType)));
                return true;
            }
            return false;
        }

        internal abstract void PrepareMapping();
        public abstract object createInstance(object src);
        
        public object Map(object src)
        {
            object dst = createInstance(src);
            foreach (IGetterAndSetter gs in GettersAndSetters)
                gs.ApplyTo(src, dst);

            return dst;
        }

        public void Match(string nameFrom, string nameTo)
        {
            MemberInfo[] miDsts = KlassDst.GetMember(nameTo);
            foreach (MemberInfo miSrc in KlassSrc.GetMember(nameFrom))
            {
                if (TryMapping(miSrc, miDsts, true, true))
                    break;
            }
        }

        public void For<R>(string nameTo, Func<R> func)
        {
            IMemberGetter getter = new FuncGetter<R>(func);
            TryMapping(getter, KlassDst.GetMember(nameTo), true, false);
        }
    }

    public abstract class MemberToMemberMapping : MemberMapping
    {

        public MemberToMemberMapping(Type klassSrc, Type klassDst, bool isToMapFields, bool isToMapProperties, Type attributeTypeToMap)
            : base(klassSrc, klassDst, isToMapFields, isToMapProperties, attributeTypeToMap){ }

        internal override void PrepareMapping()
        {
            foreach (MemberInfo miSrc in KlassSrc.GetMembers())
            {
                TryMapping(miSrc, KlassDst.GetMember(miSrc.Name));
            }
        }

        public static IMemberMapping CreateForValueType(Type klassSrc, Type klassDst, bool isToMapProperties = true, bool isToMapFields = false, Type attributeTypeToMap = null)
        {
            MemberMapping mapping = ValueTypeMemberToMemberMappingCreator.For(klassSrc, klassDst, isToMapFields, isToMapProperties, attributeTypeToMap);
            mapping.PrepareMapping();
            return mapping;
        }

        public static IMemberMapping CreateForObject(Type klassSrc, Type klassDst, ConstructorInfo ci, bool isToMapProperties = true, bool isToMapFields = false, Type attributeTypeToMap = null)
        {
            MemberMapping mapping = ObjectMemberToMemberMappingCreator.For(klassSrc, klassDst, isToMapFields, isToMapProperties, attributeTypeToMap, ci);
            mapping.PrepareMapping();
            return mapping;
        }
    }

    public abstract class MemberToMemberMappingCreator : AssemblyCreator
    {
        static Type[] MSConstructorParametersTypes = new Type[] {
            typeof(Type),
            typeof(Type),
            typeof(bool),
            typeof(bool),
            typeof(Type)
        };

        internal MethodBuilder mbGenerate;
        internal ILGenerator generateIL;

        internal MemberToMemberMappingCreator(AssemblyName aName) : base(aName, typeof(MemberToMemberMapping))
        {
            createConstructor();
            createCreateInstanceMethod();
        }

        /// <summary>
        /// Creates the constructor for the newly created Type of MemberGenerator
        /// </summary>
        private void createConstructor()
        {
            ConstructorBuilder ctor0 = tb.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                MSConstructorParametersTypes);
            ILGenerator ctor0IL = ctor0.GetILGenerator();
            ctor0IL.Emit(OpCodes.Ldarg_0);
            ctor0IL.Emit(OpCodes.Ldarg_1);
            ctor0IL.Emit(OpCodes.Ldarg_2);
            ctor0IL.Emit(OpCodes.Ldarg_3);
            ctor0IL.Emit(OpCodes.Ldarg_S, 4);
            ctor0IL.Emit(OpCodes.Ldarg_S, 5);
            ctor0IL.Emit(OpCodes.Call, typeof(MemberToMemberMapping).GetConstructor(MSConstructorParametersTypes));
            ctor0IL.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// creates the <c>CreateInstance(Object dst)</c> method for the newly created <c>MemberToMemberMapping</c> Type
        /// </summary>
        private void createCreateInstanceMethod()
        {
            mbGenerate = tb.DefineMethod(
                "createInstance",
                MethodAttributes.Public
                    | MethodAttributes.Virtual
                    | MethodAttributes.ReuseSlot
                    | MethodAttributes.HideBySig,
                typeof(Object),
                new Type[] { typeof(Object) });
            generateIL = mbGenerate.GetILGenerator();
        }

        internal MemberMapping createInstance(Type klassSrc, Type klassDst, bool isToMapFields, bool isToMapProperties, Type attributeTypeToMap)
        {
            Type t = tb.CreateType();
            ab.Save(aName.Name + ".dll");
            ConstructorInfo ci = t.GetConstructor(MSConstructorParametersTypes);
            return (MemberMapping)ci.Invoke(new object[] { klassSrc, klassDst, isToMapFields, isToMapProperties, attributeTypeToMap });
        }
    }

    public class ValueTypeMemberToMemberMappingCreator : MemberToMemberMappingCreator
    {
        internal ValueTypeMemberToMemberMappingCreator(AssemblyName aName) : base(aName) { }

        private MemberMapping create(Type klassSrc, Type klassDst, bool isToMapFields, bool isToMapProperties, Type attributeTypeToMap)
        {
            generateIL.DeclareLocal(klassDst);
            generateIL.Emit(OpCodes.Ldloca_S, 0);
            generateIL.Emit(OpCodes.Initobj, klassDst);
            generateIL.Emit(OpCodes.Ldloc_0);
            generateBoxCode(generateIL, klassDst);
            generateIL.Emit(OpCodes.Ret);
            return createInstance(klassSrc, klassDst, isToMapFields, isToMapProperties, attributeTypeToMap);
        }

        public static MemberMapping For(Type klassSrc, Type klassDst, bool isToMapFields, bool isToMapProperties, Type attributeTypeToMap)
        {
            return new ValueTypeMemberToMemberMappingCreator(
                new AssemblyName("DAIMemberMappingFor_" + klassDst.Name))
                .create(klassSrc, klassDst, isToMapFields, isToMapProperties, attributeTypeToMap);
        }
    }

    public class ObjectMemberToMemberMappingCreator : MemberToMemberMappingCreator
    {
        internal ObjectMemberToMemberMappingCreator(AssemblyName aName) : base(aName) { }

        private MemberMapping create(Type klassSrc, Type klassDst, bool isToMapFields, bool isToMapProperties, Type attributeTypeToMap, ConstructorInfo ci)
        {
            generateIL.Emit(OpCodes.Newobj, ci);
            generateIL.Emit(OpCodes.Ret);
            return createInstance(klassSrc, klassDst, isToMapFields, isToMapProperties, attributeTypeToMap);
        }

        public static MemberMapping For(Type klassSrc, Type klassDst, bool isToMapFields, bool isToMapProperties, Type attributeTypeToMap, ConstructorInfo ci)
        {
            return new ObjectMemberToMemberMappingCreator(
                new AssemblyName("DAIMemberMappingFor_" + klassDst.Name))
                .create(klassSrc, klassDst, isToMapFields, isToMapProperties, attributeTypeToMap, ci);
        }
    }

    public abstract class MemberToConstructorMapping : MemberToMemberMapping
    {
        IMemberGetter[] Getters { get; }
        ConstructorInfo ConstructorInfo;

        public MemberToConstructorMapping(Type klassSrc, Type klassDst, bool isToMapFields, bool isToMapProperties, Type attributeTypeToMap, ConstructorInfo constructorInfo)
            : base (klassSrc, klassDst, isToMapFields, isToMapProperties, attributeTypeToMap)
        {
            ConstructorInfo = constructorInfo;
            Getters = new IMemberGetter[ConstructorInfo.GetParameters().Length];
        }

        public object[] GetConstrutorParameters(object src)
        {
            object[] parameters = new object[Getters.Length];
            for (int i = 0; i < Getters.Length; i++)
            {
                parameters[i] = Getters[i].GetValue(src);
            }
            return parameters;
        }

        private static ConstructorInfo FindTheBestConstructor(Type KlassDst)
        {
            ConstructorInfo theBest = null;
            foreach (ConstructorInfo ci in KlassDst.GetConstructors())
            {
                if (theBest == null || theBest.GetParameters().Length < ci.GetParameters().Length)
                    theBest = ci;
            }
            return theBest;
        }

        internal override void PrepareMapping()
        {
            ParameterInfo[] pis = ConstructorInfo.GetParameters();
            for (int i = 0; i < pis.Length; i++)
            {
                ParameterInfo piDst = pis[i];
                foreach (MemberInfo miSrc in KlassSrc.GetMember(piDst.Name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance))
                {
                    IMemberGetter getter = CreateGetter(miSrc);
                    if (getter != null)
                    {
                        if (piDst.ParameterType.IsAssignableFrom(getter.GetterType))
                            Getters[i] = getter;
                    }
                }
                if(Getters[i] == null)
                    Getters[i] = new DefaultGetter(pis[i]);
            }
        }

        public static IMemberMapping Create(Type klassSrc, Type klassDst, bool isToMapProperties = true, bool isToMapFields = false, Type attributeTypeToMap = null)
        {
            ConstructorInfo constructorInfo = FindTheBestConstructor(klassDst);
            if (constructorInfo == null || constructorInfo.GetParameters().Length <= 0)
            {
                throw new NotSupportedException("MemberToConstructorMapping only apply to objects with public construtors of one or more parameters");
            }
            MemberMapping mapping = MemberToConstructorMappingCreator.For(klassSrc, klassDst, isToMapFields, isToMapProperties, attributeTypeToMap, constructorInfo);
            mapping.PrepareMapping();
            return mapping;
        }
    }

    public class MemberToConstructorMappingCreator : AssemblyCreator
    {
        static Type[] MSConstructorParametersTypes = new Type[] {
            typeof(Type),
            typeof(Type),
            typeof(bool),
            typeof(bool),
            typeof(Type),
            typeof(ConstructorInfo)
        };

        internal MethodBuilder mbGenerate;
        internal ILGenerator generateIL;

        internal MemberToConstructorMappingCreator(AssemblyName aName) : base(aName, typeof(MemberToConstructorMapping))
        {
            createConstructor();
            createCreateInstanceMethod();
        }

        /// <summary>
        /// Creates the constructor for the newly created Type of MemberGenerator
        /// </summary>
        private void createConstructor()
        {
            ConstructorBuilder ctor0 = tb.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                MSConstructorParametersTypes);
            ILGenerator ctor0IL = ctor0.GetILGenerator();
            ctor0IL.Emit(OpCodes.Ldarg_0);
            ctor0IL.Emit(OpCodes.Ldarg_1);
            ctor0IL.Emit(OpCodes.Ldarg_2);
            ctor0IL.Emit(OpCodes.Ldarg_3);
            ctor0IL.Emit(OpCodes.Ldarg_S, 4);
            ctor0IL.Emit(OpCodes.Ldarg_S, 5);
            ctor0IL.Emit(OpCodes.Ldarg_S, 6);
            ctor0IL.Emit(OpCodes.Call, typeof(MemberToConstructorMapping).GetConstructor(MSConstructorParametersTypes));
            ctor0IL.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// creates the <c>CreateInstance(Object dst)</c> method for the newly created <c>MemberToMemberMapping</c> Type
        /// </summary>
        private void createCreateInstanceMethod()
        {
            mbGenerate = tb.DefineMethod(
                "createInstance",
                MethodAttributes.Public
                    | MethodAttributes.Virtual
                    | MethodAttributes.ReuseSlot
                    | MethodAttributes.HideBySig,
                typeof(Object),
                new Type[] { typeof(Object) });
            generateIL = mbGenerate.GetILGenerator();
        }

        internal MemberMapping createInstance(Type klassSrc, Type klassDst, bool isToMapFields, bool isToMapProperties, Type attributeTypeToMap, ConstructorInfo destCi)
        {
            Type t = tb.CreateType();
            ab.Save(aName.Name + ".dll");
            ConstructorInfo ci = t.GetConstructor(MSConstructorParametersTypes);
            return (MemberMapping)ci.Invoke(new object[] { klassSrc, klassDst, isToMapFields, isToMapProperties, attributeTypeToMap, destCi });
        }
        
        private MemberMapping create(Type klassSrc, Type klassDst, bool isToMapFields, bool isToMapProperties, Type attributeTypeToMap, ConstructorInfo ci)
        {
            ParameterInfo[] parameters = ci.GetParameters();
            generateIL.DeclareLocal(typeof(object[]));

            generateIL.Emit(OpCodes.Ldarg_0);
            generateIL.Emit(OpCodes.Ldarg_1);
            generateIL.Emit(OpCodes.Callvirt, typeof(MemberToConstructorMapping).GetMethod("GetConstrutorParameters", new Type[] { typeof(Type) }));
            generateIL.Emit(OpCodes.Stloc_0);
            for(int i = 0; i < parameters.Length; i++)
            {
                generateIL.Emit(OpCodes.Ldloc_0);
                generateIL.Emit(OpCodes.Ldc_I4, i);
                generateIL.Emit(OpCodes.Ldelem, typeof(Object));
                generateCastAndUnboxAnyCode(generateIL, parameters[i].ParameterType);
            }
            generateIL.Emit(OpCodes.Newobj, ci);
            generateIL.Emit(OpCodes.Ret);
            return createInstance(klassSrc, klassDst, isToMapFields, isToMapProperties, attributeTypeToMap, ci);
        }

        public static MemberMapping For(Type klassSrc, Type klassDst, bool isToMapFields, bool isToMapProperties, Type attributeTypeToMap, ConstructorInfo ci)
        {
            return new MemberToConstructorMappingCreator(
                new AssemblyName("DAIMemberMappingFor_" + klassDst.Name))
                .create(klassSrc, klassDst, isToMapFields, isToMapProperties, attributeTypeToMap, ci);
        }
    }

    public interface IGetterAndSetter
    {
        void ApplyTo(object src, object dst);
    }

    public class GetterAndSetter : IGetterAndSetter
    {
        IMemberGetter Getter { get; }
        IMemberSetter Setter { get; }

        public GetterAndSetter(IMemberGetter getter, IMemberSetter setter)
        {
            Getter = getter;
            Setter = setter;
        }
        
        public void ApplyTo(object src, object dst)
        {
            Setter.SetValue(dst, Getter.GetValue(src));
        }
    }

    public class MapperGetterAndSetter : IGetterAndSetter
    {
        IMapper Mapper { get; }
        IMemberGetter Getter { get; }
        IMemberSetter Setter { get; }

        public MapperGetterAndSetter(IMemberGetter getter, IMemberSetter setter, IMapper mapper)
        {
            Getter = getter;
            Setter = setter;
            Mapper = mapper;
        }
        
        public void ApplyTo(object src, object dst)
        {
            Setter.SetValue(dst, Mapper.Map(Getter.GetValue(src)));
        }
    }

    public interface IMemberGetter
    {
        Type GetterType { get; }
        object GetValue(object obj);
    }

    public abstract class MemberGetter : IMemberGetter
    {
        public Type GetterType { get; }
        public MemberGetter(Type getterType)
        {
            GetterType = getterType;
        }

        public abstract object GetValue(object obj);
    }

    public class DefaultGetter : IMemberGetter
    {
        ParameterInfo ParameterInfo { get; set; }
        Object Value { get; }

        public DefaultGetter(ParameterInfo pi)
        {
            ParameterInfo = pi;
            Value = ParameterInfo.HasDefaultValue ? ParameterInfo.DefaultValue : null;
        }

        public object GetValue(object obj)
        {
            return Value;
        }

        public Type GetterType
        {
            get
            {
                return ParameterInfo.ParameterType;
            }
        }
    }

    public class FuncGetter<R> : MemberGetter
    {
        public Func<R> Func { get; }
        public FuncGetter(Func<R> func)
            :base(typeof(R))
        {
            Func = func;
        }

        public override object GetValue(object obj)
        {
            return (object)Func();
        }
    }

    public class MemberGetterCreator : AssemblyCreator
    {

        static Type[] MSConstructorParametersTypes = new Type[] { typeof(Type) };

        MethodBuilder mbGenerate;
        ILGenerator generateIL;

        private MemberGetterCreator(AssemblyName aName) : base(aName, typeof(MemberGetter))
        {
            createConstructor();
            createGenerateMethod();
        }

        /// <summary>
        /// Creates the constructor for the newly created Type of MemberGenerator
        /// </summary>
        private void createConstructor()
        {
            ConstructorBuilder ctor0 = tb.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                MSConstructorParametersTypes);
            ILGenerator ctor0IL = ctor0.GetILGenerator();
            ctor0IL.Emit(OpCodes.Ldarg_0);
            ctor0IL.Emit(OpCodes.Ldarg_1);
            ctor0IL.Emit(OpCodes.Call, typeof(MemberGetter).GetConstructor(MSConstructorParametersTypes));
            ctor0IL.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// creates the <c>GetValue(Object dst)</c> method for the newly created <c>MemberGetter</c> Type
        /// </summary>
        private void createGenerateMethod()
        {
            mbGenerate = tb.DefineMethod(
                "GetValue",
                MethodAttributes.Public
                    | MethodAttributes.Virtual
                    | MethodAttributes.ReuseSlot
                    | MethodAttributes.HideBySig,
                typeof(Object),
                new Type[] { typeof(Object) });
            generateIL = mbGenerate.GetILGenerator();
        }

        /// <summary>
        /// emits the code to load and cast to <paramref name="destType"/> the dest argument of Generate Method
        /// call thew New method of the Fixture property. Casts the returned value to <paramref name="castType"/>
        /// and unbox the value if <paramref name="castType"/> is a <c>PrimitiveType</c>
        /// </summary>
        /// <param name="destType"></param>
        /// <param name="castType"></param>
        private void emitTheInitialSetValueMethodCode(Type destType)
        {
            generateIL.Emit(OpCodes.Ldarg_1);
            generateCastAndUnboxCode(generateIL, destType);
        }

        private IMemberGetter create(FieldInfo fi)
        {
            emitTheInitialSetValueMethodCode(fi.DeclaringType);
            generateIL.Emit(OpCodes.Ldfld, fi);
            generateBoxCode(generateIL, fi.FieldType);
            generateIL.Emit(OpCodes.Ret);

            return createInstance(fi.FieldType);
        }

        private IMemberGetter create(PropertyInfo pi)
        {
            emitTheInitialSetValueMethodCode(pi.DeclaringType);
            generateIL.Emit(pi.DeclaringType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, pi.GetGetMethod());
            generateBoxCode(generateIL, pi.PropertyType);
            generateIL.Emit(OpCodes.Ret);

            return createInstance(pi.PropertyType);
        }

        private IMemberGetter createInstance(Type getterType)
        {
            Type t = tb.CreateType();
            ab.Save(aName.Name + ".dll");
            ConstructorInfo ci = t.GetConstructor(MSConstructorParametersTypes);
            return (IMemberGetter)ci.Invoke(new object[] { getterType });
        }

        public static IMemberGetter For(PropertyInfo pi)
        {
            return new MemberGetterCreator(
                new AssemblyName("DAIMemberGetterForPrope_" + pi.ReflectedType.Name + "." + pi.Name))
                .create(pi);
        }

        public static IMemberGetter For(FieldInfo fi)
        {
            return new MemberGetterCreator(
                new AssemblyName("DAIMemberGetterForField_" + fi.ReflectedType.Name + "." + fi.Name))
                .create(fi);
        }
    }

    public interface IMemberSetter
    {
        Type SetterType { get; }
        void SetValue(object obj, object value);
    }

    public abstract class MemberSetter : IMemberSetter
    {
        public Type SetterType { get; }
        public MemberSetter(Type setterType)
        {
            SetterType = setterType;
        }

        public abstract void SetValue(object obj, object value);
    }

    public class MemberSetterCreator : AssemblyCreator {

        static Type[] MSConstructorParametersTypes = new Type[] { typeof(Type) };

        MethodBuilder mbGenerate;
        ILGenerator generateIL;

        private MemberSetterCreator(AssemblyName aName) : base(aName, typeof(MemberSetter))
        {
            createConstructor();
            createSetValueMethod();
        }

        /// <summary>
        /// Creates the constructor for the newly created Type of MemberGenerator
        /// </summary>
        private void createConstructor()
        {
            ConstructorBuilder ctor0 = tb.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                MSConstructorParametersTypes);
            ILGenerator ctor0IL = ctor0.GetILGenerator();
            ctor0IL.Emit(OpCodes.Ldarg_0);
            ctor0IL.Emit(OpCodes.Ldarg_1);
            ctor0IL.Emit(OpCodes.Call, typeof(MemberSetter).GetConstructor(MSConstructorParametersTypes));
            ctor0IL.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// creates the <c>SetValue(Object dst)</c> method for the newly created <c>MemberSetter</c> Type
        /// </summary>
        private void createSetValueMethod()
        {
            mbGenerate = tb.DefineMethod(
                "SetValue",
                MethodAttributes.Public
                    | MethodAttributes.Virtual
                    | MethodAttributes.ReuseSlot
                    | MethodAttributes.HideBySig,
                null,
                new Type[] { typeof(Object), typeof(Object) });
            generateIL = mbGenerate.GetILGenerator();
        }

        /// <summary>
        /// emits the code to load and cast to <paramref name="destType"/> the dest argument of Generate Method
        /// call thew New method of the Fixture property. Casts the returned value to <paramref name="castType"/>
        /// and unbox the value if <paramref name="castType"/> is a <c>PrimitiveType</c>
        /// </summary>
        /// <param name="destType"></param>
        /// <param name="castType"></param>
        private void emitTheInitialSetValueMethodCode(Type destType, Type castType)
        {
            generateIL.Emit(OpCodes.Ldarg_1);
            generateCastAndUnboxCode(generateIL, destType);
            generateIL.Emit(OpCodes.Ldarg_2);
            generateCastAndUnboxAnyCode(generateIL, castType);
        }
        
        private IMemberSetter create(FieldInfo fi)
        {
            emitTheInitialSetValueMethodCode(fi.DeclaringType, fi.FieldType);
            generateIL.Emit(OpCodes.Stfld, fi);
            generateIL.Emit(OpCodes.Ret);

            return createInstance(fi.FieldType);
        }
        
        private IMemberSetter create(PropertyInfo pi)
        {
            emitTheInitialSetValueMethodCode(pi.DeclaringType, pi.PropertyType);
            generateIL.Emit(pi.DeclaringType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, pi.GetSetMethod());
            generateIL.Emit(OpCodes.Ret);

            return createInstance(pi.PropertyType);
        }
        
        private IMemberSetter createInstance(Type setterType)
        {
            Type t = tb.CreateType();
            ab.Save(aName.Name + ".dll");
            ConstructorInfo ci = t.GetConstructor(MSConstructorParametersTypes);
            return (IMemberSetter)ci.Invoke(new object[] { setterType  });
        }
        
        public static IMemberSetter For(PropertyInfo pi)
        {
            return new MemberSetterCreator(
                new AssemblyName("DAIMemberSetterForPrope_" + pi.ReflectedType.Name + "." + pi.Name))
                .create(pi);
        }
        
        public static IMemberSetter For(FieldInfo fi)
        {
            return new MemberSetterCreator(
                new AssemblyName("DAIMemberSetterForField_" + fi.ReflectedType.Name + "." + fi.Name))
                .create(fi);
        }

        public static IMemberSetter For(MemberInfo mi)
        {
            if (mi.MemberType == MemberTypes.Property)
                return MemberSetterCreator.For((PropertyInfo)mi);
            else if (mi.MemberType == MemberTypes.Field)
                return MemberSetterCreator.For((FieldInfo)mi);
            return null;
        }
    }
}
