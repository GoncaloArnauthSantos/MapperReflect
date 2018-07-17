namespace MapperReflect.Test
{
    public class Teacher
    {
        public Course[] LecturedCourses { get; set; }
        public string Name { get; set; }
        public int Id { get; set; }

        public Teacher(int nr, string name)
        {
            this.Name = name;
            this.Id = nr;
        }
    }
}
