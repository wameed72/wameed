using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormFlow.Web.Data;
using FormFlow.Web.Models;
using FormFlow.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FormFlow.Tests
{
    public class FormWorkflowServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly FormFlowDbContext _db;
        private readonly FormWorkflowService _workflow;

        public FormWorkflowServiceTests()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<FormFlowDbContext>()
                .UseSqlite(_connection)
                .Options;

            _db = new FormFlowDbContext(options);
            _db.Database.EnsureCreated();
            _workflow = new FormWorkflowService(_db);
        }

        [Fact]
        public async Task CreateSubmission_MovesCaseToSupervisorStage()
        {
            var template = await AddTemplateAsync();
            var employeeStage = template.Stages.First();

            var submission = await _workflow.CreateSubmissionAsync(
                template,
                "أحمد",
                "ahmed@example.com",
                new Dictionary<int, string> { [employeeStage.Fields[0].Id] = "10" });

            var reloaded = await _workflow.GetSubmissionAsync(submission.Id);
            Assert.Equal(2, reloaded.CurrentStageOrder);
            Assert.Equal(SubmissionStatus.InProgress, reloaded.Status);
            Assert.Equal(StageRole.Supervisor, FormWorkflowService.CurrentStage(reloaded).Role);
            Assert.StartsWith("FRM-", reloaded.TrackingCode);
        }

        [Fact]
        public async Task CompletingLastStage_MarksSubmissionCompleted()
        {
            var template = await AddTemplateAsync();
            var submission = await CreateAsync(template);

            var loaded = await _workflow.GetSubmissionAsync(submission.Id);
            var supervisorStage = FormWorkflowService.CurrentStage(loaded);

            await _workflow.CompleteStageAsync(
                loaded,
                supervisorStage,
                new Dictionary<int, string> { [supervisorStage.Fields[0].Id] = "موافقة" },
                "المشرف",
                "تم الاعتماد");

            var reloaded = await _workflow.GetSubmissionAsync(submission.Id);
            Assert.Equal(SubmissionStatus.Completed, reloaded.Status);
            Assert.Equal("موافقة", reloaded.Values.Single(v => v.FormFieldId == supervisorStage.Fields[0].Id).Value);
            Assert.Contains(reloaded.Events, e => e.Note == "تم الاعتماد");
        }

        [Fact]
        public async Task ReturnToEmployee_SendsCaseBackToFirstStage()
        {
            var template = await AddTemplateAsync();
            var submission = await CreateAsync(template);
            var loaded = await _workflow.GetSubmissionAsync(submission.Id);

            await _workflow.ReturnToEmployeeAsync(loaded, "المشرف", "البيانات ناقصة");

            var reloaded = await _workflow.GetSubmissionAsync(submission.Id);
            Assert.Equal(SubmissionStatus.Returned, reloaded.Status);
            Assert.Equal(1, reloaded.CurrentStageOrder);
        }

        [Fact]
        public async Task Inbox_ListsOnlyCasesWaitingForTheGivenRole()
        {
            var template = await AddTemplateAsync();
            await CreateAsync(template);

            var supervisorInbox = await _workflow.GetInboxAsync(StageRole.Supervisor);
            var managerInbox = await _workflow.GetInboxAsync(StageRole.Manager);

            Assert.Single(supervisorInbox);
            Assert.Empty(managerInbox);
        }

        [Fact]
        public async Task ResubmittingReturnedCase_OverwritesEmployeeAnswers()
        {
            var template = await AddTemplateAsync();
            var submission = await CreateAsync(template);
            var loaded = await _workflow.GetSubmissionAsync(submission.Id);
            await _workflow.ReturnToEmployeeAsync(loaded, "المشرف", "صحح عدد الأيام");

            var returned = await _workflow.GetSubmissionAsync(submission.Id);
            var employeeStage = returned.FormTemplate.Stages.First();
            await _workflow.CompleteStageAsync(
                returned,
                employeeStage,
                new Dictionary<int, string> { [employeeStage.Fields[0].Id] = "3" },
                "أحمد",
                "إعادة إرسال بعد التعديل");

            var reloaded = await _workflow.GetSubmissionAsync(submission.Id);
            Assert.Equal(SubmissionStatus.InProgress, reloaded.Status);
            Assert.Equal(2, reloaded.CurrentStageOrder);
            Assert.Equal("3", reloaded.Values.Single(v => v.FormFieldId == employeeStage.Fields[0].Id).Value);
        }

        [Fact]
        public async Task Validate_ReportsMissingRequiredAndBadValues()
        {
            var template = await AddTemplateAsync();
            var stage = template.Stages.First();
            var numberField = stage.Fields[0];

            var missing = FormWorkflowService.Validate(stage, new Dictionary<int, string>());
            Assert.Equal("هذا الحقل مطلوب", missing[numberField.Id]);

            var badNumber = FormWorkflowService.Validate(stage, new Dictionary<int, string> { [numberField.Id] = "abc" });
            Assert.Equal("أدخل رقمًا صحيحًا", badNumber[numberField.Id]);

            var valid = FormWorkflowService.Validate(stage, new Dictionary<int, string> { [numberField.Id] = "5" });
            Assert.Empty(valid);
            await Task.CompletedTask;
        }

        [Fact]
        public async Task Csv_ContainsOneRowPerSubmissionWithAllStageColumns()
        {
            var template = await AddTemplateAsync();
            var submission = await CreateAsync(template);
            var loaded = await _workflow.GetSubmissionAsync(submission.Id);

            var csv = CsvExporter.BuildCsv(loaded.FormTemplate, new[] { loaded });
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(2, lines.Length);
            Assert.Contains("عدد الأيام", lines[0]);
            Assert.Contains("القرار", lines[0]);
            Assert.Contains(loaded.TrackingCode, lines[1]);
        }

        private async Task<Submission> CreateAsync(FormTemplate template)
        {
            var employeeStage = template.Stages.First();
            return await _workflow.CreateSubmissionAsync(
                template,
                "أحمد",
                "ahmed@example.com",
                new Dictionary<int, string> { [employeeStage.Fields[0].Id] = "10" });
        }

        private async Task<FormTemplate> AddTemplateAsync()
        {
            var template = new FormTemplate
            {
                Title = "طلب إجازة",
                PublicToken = TokenGenerator.NewToken(),
                IsPublished = true,
                CreatedUtc = DateTime.UtcNow,
                Stages =
                {
                    new FormStage
                    {
                        Order = 1,
                        Title = "بيانات الموظف",
                        Role = StageRole.Employee,
                        Fields = { new FormField { Order = 1, Label = "عدد الأيام", Type = FieldType.Number, IsRequired = true } }
                    },
                    new FormStage
                    {
                        Order = 2,
                        Title = "قرار المشرف",
                        Role = StageRole.Supervisor,
                        Fields = { new FormField { Order = 1, Label = "القرار", Type = FieldType.Radio, IsRequired = true, Options = "موافقة\nعدم موافقة" } }
                    }
                }
            };

            _db.FormTemplates.Add(template);
            await _db.SaveChangesAsync();
            return await _workflow.GetTemplateAsync(template.Id);
        }

        public void Dispose()
        {
            _db.Dispose();
            _connection.Dispose();
        }
    }
}
