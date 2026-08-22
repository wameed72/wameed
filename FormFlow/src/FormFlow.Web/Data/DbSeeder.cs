using System;
using System.Linq;
using FormFlow.Web.Models;
using FormFlow.Web.Services;

namespace FormFlow.Web.Data
{
    /// <summary>Creates the database and inserts the default staff accounts plus a sample template.</summary>
    public static class DbSeeder
    {
        public static void Seed(FormFlowDbContext db)
        {
            db.Database.EnsureCreated();

            if (!db.Users.Any())
            {
                db.Users.AddRange(
                    CreateUser("admin", "مدير النظام", "Admin@123", StageRole.Manager, isAdministrator: true),
                    CreateUser("supervisor", "المشرف الأول", "Super@123", StageRole.Supervisor, isAdministrator: false),
                    CreateUser("manager", "المدير المباشر", "Manager@123", StageRole.Manager, isAdministrator: false));
                db.SaveChanges();
            }

            if (!db.FormTemplates.Any())
            {
                db.FormTemplates.Add(SampleLeaveRequest());
                db.SaveChanges();
            }
        }

        private static AppUser CreateUser(string username, string displayName, string password, StageRole role, bool isAdministrator)
        {
            var (hash, salt) = PasswordHasher.Create(password);
            return new AppUser
            {
                Username = username,
                DisplayName = displayName,
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = role,
                IsAdministrator = isAdministrator
            };
        }

        private static FormTemplate SampleLeaveRequest()
        {
            return new FormTemplate
            {
                Title = "طلب إجازة",
                Description = "يملأ الموظف بياناته ثم يعتمد المشرف الطلب.",
                PublicToken = TokenGenerator.NewToken(),
                IsPublished = true,
                CreatedUtc = DateTime.UtcNow,
                Stages =
                {
                    new FormStage
                    {
                        Order = 1,
                        Title = "بيانات الموظف",
                        Instructions = "يرجى تعبئة الحقول التالية بدقة.",
                        Role = StageRole.Employee,
                        Fields =
                        {
                            new FormField { Order = 1, Label = "الاسم الكامل", Type = FieldType.Text, IsRequired = true },
                            new FormField { Order = 2, Label = "القسم", Type = FieldType.Select, IsRequired = true, Options = "الموارد البشرية\nتقنية المعلومات\nالمالية\nالعمليات" },
                            new FormField { Order = 3, Label = "نوع الإجازة", Type = FieldType.Radio, IsRequired = true, Options = "سنوية\nمرضية\nطارئة" },
                            new FormField { Order = 4, Label = "تاريخ البداية", Type = FieldType.Date, IsRequired = true },
                            new FormField { Order = 5, Label = "عدد الأيام", Type = FieldType.Number, IsRequired = true },
                            new FormField { Order = 6, Label = "سبب الإجازة", Type = FieldType.LongText, IsRequired = false }
                        }
                    },
                    new FormStage
                    {
                        Order = 2,
                        Title = "قرار المشرف",
                        Instructions = "مراجعة الطلب واتخاذ القرار.",
                        Role = StageRole.Supervisor,
                        Fields =
                        {
                            new FormField { Order = 1, Label = "القرار", Type = FieldType.Radio, IsRequired = true, Options = "موافقة\nموافقة مشروطة\nعدم موافقة" },
                            new FormField { Order = 2, Label = "بديل الموظف أثناء الإجازة", Type = FieldType.Text, IsRequired = false },
                            new FormField { Order = 3, Label = "ملاحظات المشرف", Type = FieldType.LongText, IsRequired = false }
                        }
                    }
                }
            };
        }
    }
}
