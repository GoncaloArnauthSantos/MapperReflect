using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text;

namespace MapperReflect
{

    internal interface IMemberMapping
    {
        object Map(object src);
        void Match(string nameFrom, string nameTo);
    }

    internal abstract class MemberMapping : IMemberMapping
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
                    return new PropertyGetter((PropertyInfo)miSrc);
                else if (miSrc.MemberType == MemberTypes.Field && (ignoreMap || IsToMapFields))
                    return new FieldGetter((FieldInfo)miSrc);
            }
            return null;
        }

        internal Boolean TryMapping(MemberInfo miSrc, MemberInfo[] miDsts, bool ignoreMap = false, bool useAutoMapper = false)
        {
            IMemberGetter getter = CreateGetter(miSrc, ignoreMap);
            if (getter == null) return false;
            foreach (MemberInfo miDst in miDsts)
            {
                IMemberSetter setter = null;
                if (miDst.MemberType == MemberTypes.Property)
                    setter = new PropertySetter((PropertyInfo)miDst);
                else if (miDst.MemberType == MemberTypes.Field)
                    setter = new FieldSetter((FieldInfo)miDst);

                if (setter == null) return false;
                if (setter.SetterType.IsAssignableFrom(getter.GetterType))
                    GettersAndSetters.Add(new GetterAndSetter(getter, setter));
                else if (useAutoMapper)
                    GettersAndSetters.Add(new MapperGetterAndSetter(getter, setter, AutoMapper.Build(getter.GetterType, setter.SetterType)));
                return true;
            }
            return false;
        }

        internal abstract void PrepareMapping();
        internal abstract object createInstance(object src);

        //TODO implement using IL
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
    }

    internal class MemberToMemberMapping : MemberMapping
    {

        internal MemberToMemberMapping(Type klassSrc, Type klassDst, bool isToMapFields, bool isToMapProperties, Type attributeTypeToMap)
            : base(klassSrc, klassDst, isToMapFields, isToMapProperties, attributeTypeToMap) { }

        internal override object createInstance(object src)
        {
            return Activator.CreateInstance(KlassDst);
        }

        internal override void PrepareMapping()
        {
            foreach (MemberInfo miSrc in KlassSrc.GetMembers())
            {
                TryMapping(miSrc, KlassDst.GetMember(miSrc.Name));
            }
        }

        public static MemberToMemberMapping Create(Type klassSrc, Type klassDst, bool isToMapProperties = true, bool isToMapFields = false, Type attributeTypeToMap = null)
        {
            MemberToMemberMapping mapping = new MemberToMemberMapping(klassSrc, klassDst, isToMapFields, isToMapProperties, attributeTypeToMap);
            mapping.PrepareMapping();
            return mapping;
        }
    }

    internal class MemberToConstructorMapping : MemberToMemberMapping
    {
        IMemberGetter[] Getters { get; }
        ConstructorInfo ConstructorInfo;

        internal MemberToConstructorMapping(Type klassSrc, Type klassDst, bool isToMapFields, bool isToMapProperties, Type attributeTypeToMap, ConstructorInfo constructorInfo)
            : base(klassSrc, klassDst, isToMapFields, isToMapProperties, attributeTypeToMap)
        {
            ConstructorInfo = constructorInfo;
            Getters = new IMemberGetter[ConstructorInfo.GetParameters().Length];
        }

        internal override object createInstance(object src)
        {
            return ConstructorInfo.Invoke(GetConstrutorParameters(src));
        }

        private object[] GetConstrutorParameters(object src)
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
                if (Getters[i] == null)
                    Getters[i] = new DefaultGetter(pis[i]);
            }
        }

        public static new MemberToConstructorMapping Create(Type klassSrc, Type klassDst, bool isToMapProperties = true, bool isToMapFields = false, Type attributeTypeToMap = null)
        {
            ConstructorInfo constructorInfo = FindTheBestConstructor(klassDst);
            if (constructorInfo == null || constructorInfo.GetParameters().Length <= 0)
            {
                throw new NotSupportedException("MemberToConstructorMapping only apply to objects with public construtors of one or more parameters");
            }
            MemberToConstructorMapping mapping = new MemberToConstructorMapping(klassSrc, klassDst, isToMapFields, isToMapProperties, attributeTypeToMap, constructorInfo);
            mapping.PrepareMapping();
            return mapping;
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

    public class PropertyGetter : IMemberGetter
    {
        PropertyInfo PropertyInfo { get; set; }

        public PropertyGetter(PropertyInfo pi)
        {
            PropertyInfo = pi;
        }
        
        public object GetValue(object obj)
        {
            return PropertyInfo.GetValue(obj);
        }

        public Type GetterType
        {
            get{
                return PropertyInfo.PropertyType;
            }
        }
    }

    public class FieldGetter : IMemberGetter
    {
        FieldInfo FieldInfo { get; set; }

        public FieldGetter(FieldInfo fi)
        {
            FieldInfo = fi;
        }
        
        public object GetValue(object obj)
        {
            return FieldInfo.GetValue(obj);
        }

        public Type GetterType
        {
            get
            {
                return FieldInfo.FieldType;
            }
        }
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

    public interface IMemberSetter
    {
        Type SetterType { get; }
        void SetValue(object obj, object value);
    }

    public class PropertySetter : IMemberSetter
    {
        PropertyInfo PropertyInfo { get; set; }

        public PropertySetter(PropertyInfo pi)
        {
            PropertyInfo = pi;
        }
        
        public void SetValue(object obj, object value)
        {
            PropertyInfo.SetValue(obj, value);
        }

        public Type SetterType
        {
            get
            {
                return PropertyInfo.PropertyType;
            }
        }
    }

    public class FieldSetter : IMemberSetter
    {
        FieldInfo FieldInfo { get; set; }

        public FieldSetter(FieldInfo fi)
        {
            FieldInfo = fi;
        }
        
        public void SetValue(object obj, object value)
        {
            FieldInfo.SetValue(obj, value);
        }

        public Type SetterType
        {
            get
            {
                return FieldInfo.FieldType;
            }
        }
    }
}
