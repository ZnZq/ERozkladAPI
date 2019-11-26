using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace ERozklad
{
    public static class ERozkladAPI
    {
        public static Dictionary<DateTime, List<Lesson>> GetRozklad(int faculty_id, int course_id, int group_id, DateTime? timeStart = null, DateTime? timeEnd = null)
        {
            DateTime start = timeStart ?? DateTime.Now;
            DateTime end = timeEnd ?? DateTime.Now.AddMonths(2);

            string resp = GET($"http://e-rozklad.dut.edu.ua/timeTable/group?TimeTableForm[faculty]={faculty_id}&TimeTableForm[course]={course_id}&TimeTableForm[group]={group_id}&TimeTableForm[date1]={start:dd.MM.yyyy}&TimeTableForm[date2]={end:dd.MM.yyyy}");

            Dictionary<DateTime, List<Lesson>> lessonsList = new Dictionary<DateTime, List<Lesson>>();

            var match = Regex.Match(resp, @"(?<table><table .* id=""timeTableGroup"".*><\/table>)", RegexOptions.Multiline | RegexOptions.Singleline);
            if (match.Success)
            {
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(match.Groups["table"].Value);
                var table = htmlDoc.GetElementbyId("timeTableGroup");
                var trows = table.SelectNodes("//tr");

                List<KeyValuePair<int, TimeSpan[]>> lessonTimes = new List<KeyValuePair<int, TimeSpan[]>>();

                foreach (var row in trows)
                {
                    lessonTimes.Clear();
                    // td with lesson times
                    foreach (var spanLesson in row.ChildNodes[0].ChildNodes.Skip(2))
                    {
                        int lNumber = int.Parse(spanLesson.ChildNodes[0].InnerText.Split(' ')[0]);
                        TimeSpan lStart = TimeSpan.Parse(spanLesson.ChildNodes[1].InnerText);
                        TimeSpan lEnd = TimeSpan.Parse(spanLesson.ChildNodes[2].InnerText);
                        lessonTimes.Add(new KeyValuePair<int, TimeSpan[]>(lNumber, new[] { lStart, lEnd }));
                    }

                    foreach (var tdLessons in row.ChildNodes.Skip(1))
                    {
                        var date = DateTime.Parse(tdLessons.ChildNodes[1].InnerText);
                        List<Lesson> lessons = new List<Lesson>();

                        if (tdLessons.HasClass("closed"))
                            continue;

                        foreach (var lessonData in tdLessons.ChildNodes.Skip(2).Select((value, i) => (value, i)))
                        {
                            string dataContent = lessonData.value.Attributes["data-content"].Value;

                            if (string.IsNullOrWhiteSpace(dataContent))
                                continue;

                            string[] data = dataContent.Replace("\r\n", "").Replace("<br>", "\n").Trim('\n').Split('\n');

                            string[] lNameSplit = data[0].TrimEnd(']').Split('[');

                            LessonType type = GetLessonTypeFromString(lNameSplit[1]);

                            lessons.Add(new Lesson
                            {
                                Number = lessonTimes[lessonData.i].Key,
                                Start = date.Add(lessonTimes[lessonData.i].Value[0]),
                                End = date.Add(lessonTimes[lessonData.i].Value[1]),
                                LessonType = type,
                                Name = new Name { FullName = lNameSplit[0], ShortName = lessonData.value.ChildNodes[0].ChildNodes[1].InnerText.Split('[')[0] },
                                GroupName = data[1],
                                Hall = data[2],
                                Teacher = new Name { FullName = data[3], ShortName = lessonData.value.ChildNodes[0].ChildNodes[5].InnerText.Trim() },
                                Info = data[4]
                            });
                        }

                        lessonsList[date] = lessons;
                    }
                }
            }

            return new Dictionary<DateTime, List<Lesson>>(new SortedDictionary<DateTime, List<Lesson>>(lessonsList));
        }

        public static LessonType GetLessonTypeFromString(string text)
        {
            switch (text.ToLower())
            {
                case "пз":
                    return LessonType.Practical;
                case "лб":
                    return LessonType.Laboratory;
                case "зач":
                    return LessonType.Offset;
                case "экз":
                    return LessonType.Exam;
                default:
                    return LessonType.Lecture;
            }
        }

        public static string LessonTypeToString(LessonType type)
        {
            switch (type)
            {
                case LessonType.Practical: return "Практика";
                case LessonType.Laboratory: return "Лабораторная";
                case LessonType.Offset: return "Зачёт";
                case LessonType.Exam: return "Экзамен";
                case LessonType.Lecture: return "Лекция";
                default: return "Unknown";
            }
        }

        public static Dictionary<int, string> GetFaculties()
            => Parse(
                "http://e-rozklad.dut.edu.ua/timeTable/group",
                "TimeTableForm_faculty");

        public static Dictionary<int, string> GetCources(int faculty_id)
            => Parse(
                $"http://e-rozklad.dut.edu.ua/timeTable/group?TimeTableForm[faculty]={faculty_id}",
                "TimeTableForm_course");

        public static Dictionary<int, string> GetGroups(int faculty_id, int course_id)
            => Parse(
                $"http://e-rozklad.dut.edu.ua/timeTable/group?TimeTableForm[faculty]={faculty_id}&TimeTableForm[course]={course_id}",
                "TimeTableForm_group");

        private static Dictionary<int, string> Parse(string url, string select_id)
        {
            Dictionary<int, string> data = new Dictionary<int, string>();

            string resp = GET(url);

            var match = Regex.Match(resp, $@"id=\""{select_id}\"">(?<options>.*)<\/select>", RegexOptions.Multiline | RegexOptions.Singleline);
            if (match.Success)
            {
                string options = match.Groups["options"].Value;
                string pattern = @"<option value=\""(?<id>\d+)\"".*>(?<name>.*)<\/option>";

                foreach (Match m in Regex.Matches(options, pattern, RegexOptions.Multiline))
                {
                    int id = int.Parse(m.Groups["id"].Value);
                    string name = m.Groups["name"].Value;

                    data[id] = name;
                }
            }

            return data;
        }

        private static string GET(string uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
