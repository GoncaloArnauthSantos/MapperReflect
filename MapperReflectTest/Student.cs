namespace MapperReflect.Test
{
    public class Student
    {
        public School Org { get; set; }
        public Course[] Courses { get; set; }
        [ToMap]
        public string Name { get; set; }
        public int Nr { get; set; }
        public int Height { get; set; }
    }
    
}
