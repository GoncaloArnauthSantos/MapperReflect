using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text;

namespace MapperReflect
{
    public class AutoMapper
    {
        public class IMapperKey{
            Type KlassSrc { get; }
            Type KlassDst { get; }

            int HashCode { get; }

            public IMapperKey(Type klassSrc, Type klassDst)
            {
                KlassSrc = klassSrc;
                KlassDst = klassDst;
                HashCode = (KlassSrc.AssemblyQualifiedName + KlassSrc.FullName + KlassDst.AssemblyQualifiedName + KlassDst.FullName).GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj is IMapperKey)
                {
                    return KlassSrc == ((IMapperKey)obj).KlassSrc
                        && KlassDst == ((IMapperKey)obj).KlassDst;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return HashCode;
            }
        }

        public static Dictionary<IMapperKey, IMapper> mappers = new Dictionary<IMapperKey, IMapper>();

        public static IMapper Build(Type klassSrc, Type klassDst)
        {
            IMapperKey iMapperKey = new IMapperKey(klassSrc, klassDst);
            IMapper mapper;
            if(!mappers.TryGetValue(iMapperKey, out mapper))
            {
                mapper = new Mapper(klassSrc, klassDst);
                mappers.Add(iMapperKey, mapper);
            }
            return mapper;
        }
    }
}
