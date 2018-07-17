using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text;

namespace MapperReflect
{
    public class Mapping
    {
        Type AttributeType { get; }
        bool IsToMapProperties { get; }
        bool IsToMapFields { get; }

        public Mapping(Type attributeType)
            :this(true, true, attributeType)
        {
        }
        
        private Mapping(bool isToMapProperties, bool isToMapFields)
            :this(isToMapProperties, isToMapFields, null)
        {
        }

        private Mapping(bool isToMapProperties, bool isToMapFields, Type attributeType)
        {
            AttributeType = attributeType;
            IsToMapProperties = isToMapProperties;
            IsToMapFields = isToMapFields;
        }

        public static readonly Mapping Fields = new Mapping(false, true);
        public static readonly Mapping Properties = new Mapping(true, false);

        internal virtual IMemberMapping CreateFor(Type klassSrc, Type klassDst)
        {
            if (klassDst.GetTypeInfo().IsValueType)
            {
                return MemberToMemberMapping.CreateForValueType(klassSrc, klassDst, IsToMapProperties, IsToMapFields, AttributeType);
            }
            else
            {
                ConstructorInfo ci = klassDst.GetConstructor(Type.EmptyTypes);
                if (ci != null)
                {
                    return MemberToMemberMapping.CreateForObject(klassSrc, klassDst, ci, IsToMapProperties, IsToMapFields, AttributeType);
                }
                return MemberToConstructorMapping.Create(klassSrc, klassDst, IsToMapProperties, IsToMapFields, AttributeType);
            }
        }
    }
}
