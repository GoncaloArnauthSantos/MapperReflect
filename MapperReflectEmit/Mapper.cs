using System;
using System.Collections.Generic;
using System.Text;

namespace MapperReflect
{
    public class Mapper : IMapper
    {
        Type KlassSrc { get; }
        Type KlassDst { get; }
        Mapping Mapping { get; set; }
        IMemberMapping MemberMapping { get; set; }

        public Mapper(Type klassSrc, Type klassDst)
            :this(
                 klassSrc,
                 klassDst,
                 Mapping.Properties)
        {
        }

        private Mapper(Type klassSrc, Type klassDst, Mapping mapping)
        {
            KlassSrc = klassSrc;
            KlassDst = klassDst;
            Mapping = mapping;
            MemberMapping = mapping.CreateFor(klassSrc, klassDst);
        }

        public IMapper Bind(Mapping m)
        {
            if (m != Mapping)
            {
                Mapping = m;
                MemberMapping = m.CreateFor(KlassSrc, KlassDst);
            }
            //chosed to return the same instance because of the cache implementation
            return this;
        }

        public IMapper Match(string nameFrom, string nameDest)
        {
            MemberMapping.Match(nameFrom, nameDest);
            //chosed to return the same instance because of the cache implementation
            return this;
        }

        public IMapper For<R>(string nameTo, Func<R> func)
        {
            MemberMapping.For(nameTo, func);
            return this;
        }

        public object Map(object obj)
        {
            if (obj == null)
                return null;
            return MemberMapping.Map(obj);
        }

        public object[] Map(object[] objs)
        {
            object[] objsDst = new object[objs.Length];
            for(int i=0; i<objsDst.Length; i++)
            {
                objsDst[i] = Map(objs[i]);
            }
            return objsDst;
        }
    }

    public class Mapper<TSrc, TDest> : Mapper, IMapper<TSrc, TDest>
    {
        public Mapper() : base(typeof(TSrc), typeof(TDest)) {}

        public TDest Map(TSrc src)
        {
            return (TDest) base.Map(src);
        }

        public TDest[] Map(TSrc[] src)
        {
            //TODO use the generic method
            return (TDest[]) base.Map(src);
        }

        public E Map<E>(IEnumerable<TSrc> src) where E : ICollection<TDest>, new()
        {
            E e = new E();
            foreach(TSrc srcInstance in src)
            {
                e.Add(Map(srcInstance));
            }
            return e;
        }

        public IEnumerable<TDest> MapLazy(IEnumerable<TSrc> src)
        {
            foreach (TSrc srcInstance in src)
            {
                yield return Map(srcInstance);
            }
        }
    }
}