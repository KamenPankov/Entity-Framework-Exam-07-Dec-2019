namespace TeisterMask.DataProcessor
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    using ValidationContext = System.ComponentModel.DataAnnotations.ValidationContext;

    using Data;
    using System.Xml.Serialization;
    using TeisterMask.DataProcessor.ImportDto;
    using System.IO;
    using System.Text;
    using TeisterMask.Data.Models;
    using System.Globalization;
    using AutoMapper;
    using System.Linq;
    using TeisterMask.Data.Models.Enums;
    using Newtonsoft.Json;

    public class Deserializer
    {
        private const string ErrorMessage = "Invalid data!";

        private const string SuccessfullyImportedProject
            = "Successfully imported project - {0} with {1} tasks.";

        private const string SuccessfullyImportedEmployee
            = "Successfully imported employee - {0} with {1} tasks.";

        public static string ImportProjects(TeisterMaskContext context, string xmlString)
        {
            XmlSerializer xmlSerializer = 
                new XmlSerializer(typeof(ProjectImportDto[]), new XmlRootAttribute("Projects"));

            ProjectImportDto[] projectImportDtos = (ProjectImportDto[])xmlSerializer.Deserialize(new StringReader(xmlString));

            StringBuilder stringBuilder = new StringBuilder();

            List<Project> projects = new List<Project>();

            foreach (ProjectImportDto projectImportDto in projectImportDtos)
            {
                bool isOpenDateValid = DateTime.TryParseExact(projectImportDto.OpenDate, "dd/MM/yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime openProjectDate);

                bool isDueDateValid = true;
                if (!string.IsNullOrEmpty(projectImportDto.DueDate))
                {
                    isDueDateValid = DateTime.TryParseExact(projectImportDto.DueDate, "dd/MM/yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dueDate);
                }

                if (!isOpenDateValid || !isDueDateValid)
                {
                    stringBuilder.AppendLine(ErrorMessage);
                    continue;
                }

                Project project = new Project()
                {
                    Name = projectImportDto.Name,
                    OpenDate = DateTime.ParseExact(projectImportDto.OpenDate, "dd/MM/yyyy",
                                                    CultureInfo.InvariantCulture),
                    DueDate = string.IsNullOrEmpty(projectImportDto.DueDate) ? null :
                                (DateTime?)DateTime.ParseExact(projectImportDto.DueDate, "dd/MM/yyyy",
                                                    CultureInfo.InvariantCulture)
                };

                if (!IsValid(project))
                {
                    stringBuilder.AppendLine(ErrorMessage);
                    continue;
                }

                List<Task> tasks = new List<Task>();

                foreach (TaskImportDto taskImportDto in projectImportDto.Tasks)
                {
                    if (string.IsNullOrEmpty(taskImportDto.OpenDate) ||
                        string.IsNullOrEmpty(taskImportDto.DueDate))
                    {
                        stringBuilder.AppendLine(ErrorMessage);
                        continue;
                    }

                    bool isTaskOpenDateValid = DateTime.TryParseExact(taskImportDto.OpenDate, "dd/MM/yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime openTaskDate);

                    bool isTaskDueDateValid = DateTime.TryParseExact(taskImportDto.DueDate, "dd/MM/yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dueTaskDate);

                    bool isExecutionTypeValid = Enum.IsDefined(typeof(ExecutionType), taskImportDto.ExecutionType);
                    bool isLabelTypeValid = Enum.IsDefined(typeof(LabelType), taskImportDto.LabelType);

                    if (!isExecutionTypeValid || !isLabelTypeValid)
                    {
                        stringBuilder.AppendLine(ErrorMessage);
                        continue;
                    }

                    if (!isTaskDueDateValid || !isTaskDueDateValid)
                    {
                        stringBuilder.AppendLine(ErrorMessage);
                        continue;
                    }

                    if (openTaskDate < openProjectDate || dueTaskDate > project.DueDate)
                    {
                        stringBuilder.AppendLine(ErrorMessage);
                        continue;
                    }

                    if (taskImportDto.Name.Length < 2 || taskImportDto.Name.Length > 40)
                    {
                        stringBuilder.AppendLine(ErrorMessage);
                        continue;
                    }

                    Task task = new Task()
                    {
                        Name = taskImportDto.Name,
                        OpenDate = DateTime.ParseExact(taskImportDto.OpenDate, "dd/MM/yyyy",
                    CultureInfo.InvariantCulture),
                        DueDate = DateTime.ParseExact(taskImportDto.DueDate, "dd/MM/yyyy",
                    CultureInfo.InvariantCulture),
                        ExecutionType = (ExecutionType)taskImportDto.ExecutionType,
                        LabelType = (LabelType)taskImportDto.LabelType,
                        Project = project
                    };

                    tasks.Add(task);
                }

                project.Tasks = tasks;

                projects.Add(project);

                stringBuilder.AppendLine(string.Format(SuccessfullyImportedProject,
                    project.Name, project.Tasks.Count()));
            }

            context.Projects.AddRange(projects);
            context.SaveChanges();

            //Console.WriteLine(stringBuilder.ToString().TrimEnd());

            return stringBuilder.ToString().TrimEnd();
        }

        public static string ImportEmployees(TeisterMaskContext context, string jsonString)
        {
            EmployeeImportDto[] employeeImportDtos = JsonConvert.DeserializeObject<EmployeeImportDto[]>(jsonString);

            StringBuilder stringBuilder = new StringBuilder();

            List<Employee> employees = new List<Employee>();

            int[] tasksExist = context.Tasks.Select(t => t.Id).ToArray();

            foreach (EmployeeImportDto employeeImportDto in employeeImportDtos)
            {
                if (!IsValid(employeeImportDto))
                {
                    stringBuilder.AppendLine(ErrorMessage);
                    continue;
                }

                Employee employee = new Employee()
                {
                    Username = employeeImportDto.Username,
                    Email = employeeImportDto.Email,
                    Phone = employeeImportDto.Phone
                };

                List<EmployeeTask> employeeTasks = new List<EmployeeTask>();

                foreach (int taskImportId in employeeImportDto.Tasks.Distinct())
                {
                    bool isTaskExists = tasksExist.Any(t => t == taskImportId);

                    if (!isTaskExists)
                    {
                        stringBuilder.AppendLine(ErrorMessage);
                        continue;
                    }

                    //bool isTaskUnique = employeeTasks.Any(et => et.TaskId == taskImportId.TaskId);

                    //if (isTaskUnique)
                    //{
                    //    continue;
                    //}

                    EmployeeTask employeeTask = new EmployeeTask()
                    {
                        TaskId = taskImportId,
                        Employee = employee
                    };

                    employeeTasks.Add(employeeTask);
                }

                employee.EmployeesTasks = employeeTasks;

                employees.Add(employee);

                stringBuilder.AppendLine(string.Format(SuccessfullyImportedEmployee,
                    employee.Username, employee.EmployeesTasks.Count()));
            }

            context.Employees.AddRange(employees);
            context.SaveChanges();

            //Console.WriteLine();
            //Console.WriteLine(stringBuilder.ToString().TrimEnd());

            return stringBuilder.ToString().TrimEnd();
        }

        private static bool IsValid(object entity)
        {
            var validationContext = new ValidationContext(entity);
            var validationResult = new List<ValidationResult>();

            return Validator.TryValidateObject(entity, validationContext, validationResult, true);
        }
    }
}