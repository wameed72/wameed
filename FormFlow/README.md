# FormFlow — استمارات متعددة المراحل

تطبيق ويب (ASP.NET Core 5 + Razor Pages + EF Core) لإنشاء استمارات تشبه Google Forms، لكن مقسّمة إلى **مراحل** لكل مرحلة **دور**: الموظف يملأ مرحلته ويرسل، ثم تنتقل الحالة تلقائيًا إلى صندوق وارد المشرف ليكمل مرحلته.

## المتطلبات

- .NET 5 SDK
- Visual Studio 2019 (16.11 أو أحدث) — افتح `FormFlow.sln`

## التشغيل

```bash
cd FormFlow/src/FormFlow.Web
dotnet run
```

يُنشئ التطبيق قاعدة بيانات SQLite (`formflow.db`) ويضيف بيانات تجريبية عند أول تشغيل: مستخدمين ونموذج «طلب إجازة» من مرحلتين.

للتحويل إلى SQL Server: عدّل `ConnectionStrings:Default` في `appsettings.json` واستبدل `UseSqlite` بـ `UseSqlServer` في `Startup.cs` (مع إضافة حزمة `Microsoft.EntityFrameworkCore.SqlServer`).

## حسابات تجريبية (للتطوير فقط — غيّرها قبل الإنتاج)

| المستخدم | كلمة المرور | الدور |
| --- | --- | --- |
| `admin` | `Admin@123` | مدير + إدارة الاستمارات |
| `supervisor` | `Super@123` | مشرف |
| `manager` | `Manager@123` | مدير مباشر |

## سير العمل

1. المسؤول ينشئ الاستمارة من `/Admin/Index`، ويضيف المراحل والأسئلة من `/Admin/Edit`، ثم ينشرها.
2. يُرسل الرابط العام `/f/{token}` إلى الموظفين (بدون تسجيل دخول).
3. عند الإرسال تُنشأ حالة برمز متابعة `FRM-XXXXXX`، وتنتقل إلى المرحلة التالية.
4. صاحب دور المرحلة يجدها في `/Inbox/Index`، ويكملها أو يعيدها للموظف أو يرفضها.
5. الموظف يتابع الحالة من `/Track?code=FRM-XXXXXX`.
6. الردود تُعرض في `/Admin/Submissions` مع تصدير CSV (UTF-8 BOM ليفتح Excel العربية بشكل صحيح).

## البنية

- `src/FormFlow.Web/Models` — النموذج: `FormTemplate`، `FormStage`، `FormField`، `Submission`، `FieldValue`، `SubmissionEvent`.
- `src/FormFlow.Web/Services/FormWorkflowService.cs` — كل منطق الانتقال بين المراحل والتحقق من الإجابات.
- `src/FormFlow.Web/Pages` — الصفحات العامة (تعبئة/تتبع)، صندوق الوارد، ولوحة الإدارة.
- `tests/FormFlow.Tests` — اختبارات xUnit لسير العمل والتحقق وتصدير CSV وتشفير كلمات المرور.

## الاختبارات

```bash
cd FormFlow
dotnet test
```
