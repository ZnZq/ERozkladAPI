using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERozklad
{
    public class Lesson
    {
        public int Number { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public LessonType LessonType { get; set; }
        public Name Name { get; set; }
        public string GroupName { get; set; }
        public string Hall { get; set; }
        public Name Teacher { get; set; }
        public string Info { get; set; }
    }

    public enum LessonType
    {
        Lecture,
        Laboratory,
        Practical,
        Offset,
        Exam
    }
}
