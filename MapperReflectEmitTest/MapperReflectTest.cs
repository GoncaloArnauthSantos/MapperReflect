﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace MapperReflect.Test
{
    [TestClass]
    public class MapperReflectEmitTest
    {
        [TestMethod]
        public void TestCache()
        {
            AutoMapper.ClearCache();
            IMapper msp1 = AutoMapper
                .Build(typeof(Student), typeof(Person));
            IMapper mst1 = AutoMapper
                .Build(typeof(Student), typeof(Teacher));
            IMapper msp2 = AutoMapper
                .Build(typeof(Student), typeof(Person));
            IMapper mst2 = AutoMapper
                .Build(typeof(Student), typeof(Teacher));

            Assert.AreSame(msp1, msp2);
            Assert.AreSame(mst1, mst2);
        }

        [TestMethod]
        public void TestMapProperties_Default()
        {
            AutoMapper.ClearCache();
            IMapper m = AutoMapper
                .Build(typeof(Student), typeof(Person));
            Student s = new Student
            {
                Nr = 27721,
                Name = "Ze Manel",
                Height = 170,
                Org = new School
                {
                    Name = "ISEL",
                    MembersIds = new int[] { 31743, 40599, 40682 },
                    Location = "Lisboa, Portugal"
                }
            };
            Person p = (Person)m.Map(s);
            Assert.AreEqual(s.Name, p.Name);
            Assert.AreEqual(s.Height, p.Height);
            Assert.IsNull(p.Org);
        }

        [TestMethod]
        public void TestMapPropertiesToConstructor_Default()
        {
            AutoMapper.ClearCache();
            IMapper m = AutoMapper
                .Build(typeof(Student), typeof(Teacher));
            Student s = new Student
            {
                Nr = 27721,
                Name = "Ze Manel",
                Height = 170
            };
            Teacher t = (Teacher)m.Map(s);
            Assert.AreEqual(s.Name, t.Name);
            Assert.AreEqual(s.Nr, t.Id);
        }

        [TestMethod]
        public void TestBindProperties()
        {
            AutoMapper.ClearCache();
            IMapper m = AutoMapper
                .Build(typeof(Student), typeof(Person))
                .Bind(Mapping.Properties);
            Student s = new Student
            {
                Nr = 27721,
                Name = "Ze Manel",
                Height = 170
            };
            Person p = (Person)m.Map(s);
            Assert.AreEqual(s.Name, p.Name);
            Assert.AreEqual(s.Height, p.Height);
        }

        [TestMethod]
        public void TestMatchProperties()
        {
            AutoMapper.ClearCache();
            IMapper m = AutoMapper
                .Build(typeof(Student), typeof(Teacher))
                .Bind(Mapping.Fields)
                .Match("Nr", "Id")
                .Match("Courses", "LecturedCourses");
            Student s = new Student
            {
                Nr = 27721,
                Name = "Ze Manel",
                Height = 170
            };
            Teacher t = (Teacher)m.Map(s);
            Assert.AreNotEqual(s.Name, t.Name);
            Assert.AreEqual(s.Nr, t.Id);
            Assert.AreEqual(s.Courses, t.LecturedCourses);
        }

        public void ComplexCompareStudentWithPerson(Student s, Person p)
        {
            Assert.AreEqual(s.Name, p.Name);
            Assert.AreEqual(s.Height, p.Height);
            Assert.AreEqual(s.Nr, p.Id);
            Assert.AreEqual(s.Org.Name, p.Org.Name);
            Assert.AreEqual(s.Org.MembersIds, p.Org.MembersIds);
        }

        [TestMethod]
        public void TestMatchComplexProperties()
        {
            AutoMapper.ClearCache();
            IMapper m = AutoMapper
                .Build(typeof(Student), typeof(Person))
                .Bind(Mapping.Properties)
                .Match("Nr", "Id")
                .Match("Org", "Org");
            Student s = new Student
            {
                Nr = 27721,
                Name = "Ze Manel",
                Height = 170,
                Org = new School
                {
                    Name = "ISEL",
                    MembersIds = new int[] { 31743, 40599, 40682 },
                    Location = "Lisboa, Portugal"
                }
            };
            Person p = (Person)m.Map(s);
            ComplexCompareStudentWithPerson(s, p);
        }

        [TestMethod]
        public void TestArrayMap()
        {
            AutoMapper.ClearCache();
            IMapper m = AutoMapper
                .Build(typeof(Student), typeof(Person))
                .Bind(Mapping.Properties)
                .Match("Nr", "Id")
                .Match("Org", "Org");
            Student[] s = {
                new Student
                {
                    Nr = 27721,
                    Name = "Ze Manel",
                    Height = 170,
                    Org = new School
                    {
                        Name = "FCT",
                        MembersIds = new int[] { 1, 2, 3 },
                        Location = "Lisboa, Portugal"
                    }
                },
                new Student
                {
                    Nr = 31743,
                    Name = "Ricardo Cacheira",
                    Height = 177,
                    Org = new School
                    {
                        Name = "ISEL",
                        MembersIds = new int[] { 31743, 40599, 40682 },
                        Location = "Lisboa, Portugal"
                    }
                }
            };
            object[] p = m.Map(s);
            for (int i = 0; i < p.Length; i++)
            {
                Assert.IsInstanceOfType(p[i], typeof(Person));
                ComplexCompareStudentWithPerson(s[i], (Person)p[i]);
            }
        }

        [TestMethod]
        public void TestMapWithCustomAttributes()
        {
            AutoMapper.ClearCache();
            IMapper m = AutoMapper
                .Build(typeof(Student), typeof(Person))
                .Bind(new Mapping(typeof(ToMapAttribute)))
                .Match("Nr", "Id");
            Student s = new Student
            {
                Nr = 27721,
                Name = "Ze Manel",
                Height = 170,
                Org = new School
                {
                    Name = "FCT",
                    MembersIds = new int[] { 1, 2, 3 },
                    Location = "Lisboa, Portugal"
                }
            };
            Person p = (Person)m.Map(s);
            Assert.AreEqual(s.Name, p.Name);
            Assert.AreEqual(s.Nr, p.Id);
            Assert.AreNotEqual(s.Height, p.Height);
            Assert.IsNull(p.Org);
        }

        [TestMethod]
        public void TestStructProperties()
        {
            AutoMapper.ClearCache();
            IMapper m = AutoMapper
                .Build(typeof(Nurse), typeof(Student));

            Nurse nrs = new Nurse { Name = "D. Cristina", Nr = 1 };
            Student s = (Student)m.Map(nrs);
            Assert.AreEqual(nrs.Name, s.Name);
            Assert.AreEqual(nrs.Nr, s.Nr);
        }

        [TestMethod]
        public void TestStructPropertiesInverse()
        {
            AutoMapper.ClearCache();
            IMapper m = AutoMapper
                .Build(typeof(Student), typeof(Nurse));

            Student s = new Student
            {
                Nr = 27721,
                Name = "Ze Manel",
                Height = 170,
                Org = new School
                {
                    Name = "FCT",
                    MembersIds = new int[] { 1, 2, 3 },
                    Location = "Lisboa, Portugal"
                }
            };

            Nurse nrs = (Nurse)m.Map(s);
            Assert.AreEqual(nrs.Name, s.Name);
            Assert.AreEqual(nrs.Nr, s.Nr);
        }

        [TestMethod]
        public void TestStructWithCtrProperties()
        {
            AutoMapper.ClearCache();
            IMapper m = AutoMapper
                .Build(typeof(Student), typeof(Postman));

            Student s = new Student
            {
                Nr = 27721,
                Name = "Ze Manel",
                Height = 170,
                Org = new School
                {
                    Name = "FCT",
                    MembersIds = new int[] { 1, 2, 3 },
                    Location = "Lisboa, Portugal"
                }
            };
            Postman pstm = (Postman)m.Map(s);

            Assert.AreEqual(pstm.Name, s.Name);
            Assert.AreEqual(pstm.Nr, s.Nr);
        }

        [TestMethod]
        public void TestStructWithNothingProperties()
        {
            AutoMapper.ClearCache();
            IMapper m = AutoMapper
                .Build(typeof(Student), typeof(Nobody));

            Student s = new Student
            {
                Nr = 27721,
                Name = "Ze Manel",
                Height = 170,
                Org = new School
                {
                    Name = "FCT",
                    MembersIds = new int[] { 1, 2, 3 },
                    Location = "Lisboa, Portugal"
                }
            };
            Nobody nb = (Nobody)m.Map(s);
            Assert.IsNotNull(nb);
        }

        [TestMethod]
        public void TestMatchComplexFields()
        {
            AutoMapper.ClearCache();
            IMapper m = AutoMapper
                .Build(typeof(Doctor), typeof(Person))
                .Bind(Mapping.Fields)
                .Match("doctorNr", "Id");

            Doctor d = new Doctor { doctorNr = 213, name = "Zé Manel" };
            Person p = (Person)m.Map(d);

            Assert.AreEqual(p.Id, d.doctorNr);
        }

        [TestMethod]
        public void TestMapField_Default()
        {
            AutoMapper.ClearCache();
            IMapper m = AutoMapper
                .Build(typeof(Doctor), typeof(Patient))
                .Bind(Mapping.Fields);
                
            Doctor d = new Doctor { doctorNr = 213, name = "Zé Manel", localization = "Chelas" };
            Patient pt = (Patient)m.Map(d);

            Assert.AreEqual(pt.name, d.name);
            Assert.AreEqual(pt.localization, d.localization);
        }

        [TestMethod]
        public void TestGenericMapper()
        {
            Student[] stds = {
                new Student
                {
                    Nr = 27721,
                    Name = "Ze Manel",
                    Height = 170,
                    Org = new School
                    {
                        Name = "FCT",
                        MembersIds = new int[] { 1, 2, 3 },
                        Location = "Lisboa, Portugal"
                    }
                },
                new Student
                {
                    Nr = 31743,
                    Name = "Ricardo Cacheira",
                    Height = 177,
                    Org = new School
                    {
                        Name = "ISEL",
                        MembersIds = new int[] { 31743, 40599, 40682 },
                        Location = "Lisboa, Portugal"
                    }
                }
            };

            Mapper<Student, Person> m = (Mapper<Student, Person>)AutoMapper.Build<Student, Person>()
                .Match("Nr", "Id")
                .Match("Org", "Org");
            List<Person> ps = m.Map<List<Person>>(stds);

            for (int i = 0; i < ps.Count; i++)
            {
                Assert.IsInstanceOfType(ps[i], typeof(Person));
                ComplexCompareStudentWithPerson(stds[i], (Person)ps[i]);
            }
        }

        [TestMethod]
        public void TestGenericLazy()
        {
            Student s0 = new Student
            {
                Nr = 27721,
                Name = "Ze Manel",
                Height = 170,
                Org = new School
                {
                    Name = "FCT",
                    MembersIds = new int[] { 1, 2, 3 },
                    Location = "Lisboa, Portugal"
                }
            };

            Student s1 = new Student
            {
                Nr = 31743,
                Name = "Ricardo Cacheira",
                Height = 177,
                Org = new School
                {
                    Name = "ISEL",
                    MembersIds = new int[] { 31743, 40599, 40682 },
                    Location = "Lisboa, Portugal"
                }
            };

            IEnumerable<Student> ies = new List<Student>(){
                new Student
                {
                    Nr = 27721,
                    Name = "Ze Manel",
                    Height = 170,
                    Org = new School
                    {
                        Name = "FCT",
                        MembersIds = new int[] { 1, 2, 3 },
                        Location = "Lisboa, Portugal"
                    }
                },
                s1
            };

            Mapper<Student, Person> m = (Mapper<Student, Person>)AutoMapper.Build<Student, Person>()
                .Match("Nr", "Id")
                .Match("Org", "Org");

            IEnumerator<Student> enumStudent = ies.GetEnumerator();
            IEnumerator<Person> enumPerson = m.MapLazy(ies).GetEnumerator();

            s1.Nr = 40682;
            s1.Name = "Renato Junior";
            while (enumStudent.MoveNext() && enumPerson.MoveNext())
            {
                Assert.IsInstanceOfType(enumPerson.Current, typeof(Person));
                ComplexCompareStudentWithPerson(enumStudent.Current, enumPerson.Current);
            }

            s0.Nr = 1111;
            s1.Nr = 1111;

            while (enumStudent.MoveNext() && enumPerson.MoveNext())
            {
                Assert.IsInstanceOfType(enumPerson.Current, typeof(Person));
                Assert.AreNotEqual(enumPerson.Current.Id, enumStudent.Current.Nr);
            }
        }


        [TestMethod]
        public void TestMapperFor()
        {
            Student s = new Student
            {
                Nr = 27721,
                Name = "Ze Manel",
                Height = 170,
                Org = new School
                {
                    Name = "FCT",
                    MembersIds = new int[] { 1, 2, 3 },
                    Location = "Lisboa, Portugal"
                }
            };

            Mapper<Student, Person> m = (Mapper<Student, Person>)AutoMapper.Build<Student, Person>()
                .Match("Nr", "Id")
                .Match("Org", "Org")
                .For("Name", () => { return "Ricardo"; });

            Person p = m.Map(s);
            Assert.AreEqual(p.Name, "Ricardo");
        }
    }
}
