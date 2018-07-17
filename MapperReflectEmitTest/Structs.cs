using System;
using System.Collections.Generic;
using System.Text;

namespace MapperReflect.Test
{
    public struct Postman
    {
        public string Name { get; set; }
        public int Nr { get; set; }

        public Postman(string name, int nr)
        {
            Name = name;
            Nr = nr;
        }
    }
    public struct Nurse
    {
        public string Name { get; set; }
        public int Nr { get; set; }
    }
    public struct Nobody { }
}
