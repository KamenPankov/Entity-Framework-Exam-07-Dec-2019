namespace TeisterMask.DataProcessor
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml.Serialization;
    using Data;
    using Newtonsoft.Json;
    using TeisterMask.DataProcessor.ExportDto;
    using Formatting = Newtonsoft.Json.Formatting;

    public class Serializer
    {
        public static string ExportProjectWithTheirTasks(TeisterMaskContext context)
        {
            ProjectExportDto[] projectsWithTasks = context.Projects
                 .Where(p => p.Tasks.Any())
                 .Select(p => new ProjectExportDto()
                 {
                     TasksCount = p.Tasks.Count(),
                     ProjectName = p.Name,
                     HasEndDate = p.DueDate == null ? "No" : "Yes",
                     Tasks = p.Tasks.Select(t => new TaskExportDto()
                     {
                         Name = t.Name,
                         Label = t.LabelType.ToString()
                     })
                     .OrderBy(t => t.Name)
                     .ToArray()
                 })
                 .OrderByDescending(p => p.TasksCount)
                 .ThenBy(p => p.ProjectName)
                 .ToArray();

            XmlSerializer xmlSerializer = new XmlSerializer(typeof(ProjectExportDto[]), new XmlRootAttribute("Projects"));

            XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces();
            namespaces.Add(string.Empty, string.Empty);

            StringBuilder stringBuilder = new StringBuilder();

            xmlSerializer.Serialize(new StringWriter(stringBuilder), projectsWithTasks, namespaces);

            return stringBuilder.ToString().TrimEnd();
        }

        public static string ExportMostBusiestEmployees(TeisterMaskContext context, DateTime date)
        {
            var employees = context.Employees
                .Where(e => e.EmployeesTasks.Any(t => t.Task.OpenDate >= date))
                .Select(e => new
                {
                    Username = e.Username,
                    Tasks = e.EmployeesTasks
                    .Where(t => t.Task.OpenDate >= date)
                    .OrderByDescending(t => t.Task.DueDate)
                    .ThenBy(t => t.Task.Name)
                    .Select(t => new
                    {
                        TaskName = t.Task.Name,
                        OpenDate = t.Task.OpenDate.ToString("d", CultureInfo.InvariantCulture),
                        DueDate = t.Task.DueDate.ToString("d", CultureInfo.InvariantCulture),
                        LabelType = t.Task.LabelType.ToString(),
                        ExecutionType = t.Task.ExecutionType.ToString()
                    })
                    .ToArray()
                })  
                .ToArray()
                .OrderByDescending(e => e.Tasks.Count())
                .ThenBy(e => e.Username)
                .Take(10)
                .ToArray();

            string jsonString = JsonConvert.SerializeObject(employees, Formatting.Indented);

            return jsonString;
        }
    }
}