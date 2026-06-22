# طبيب أخطاء ASP.NET Core — ASP.NET Core Error Doctor

تطبيق سطح مكتب (WPF / .NET 8) يعمل **بدون إنترنت (Offline)**، يساعد مطوّري ASP.NET Core على
تشخيص الأخطاء بسرعة: تلصق رسالة الخطأ أو الـ Stack Trace، فيقترح التطبيق **السبب المحتمل والحل**
من قاعدة بيانات **SQL Server** محلية. تُحدَّث قاعدة البيانات تلقائياً (يومياً/أسبوعياً) عند توفّر الإنترنت.

> A WPF (.NET 8) **offline-first** desktop app that diagnoses ASP.NET Core errors. Paste an error
> message or stack trace and it suggests the likely cause and fix from a local **SQL Server**
> knowledge base that auto-updates when the internet is available.

---

## المكوّنات (Solution layout)

| المشروع | الوصف |
| --- | --- |
| `src/ErrorDoctor.Core` | المنطق الأساسي: نماذج البيانات، طبقة EF Core / SQL Server، محرّك مطابقة الأخطاء، خدمة التحديث، ومجموعة الأخطاء الأولية المضمّنة. |
| `src/ErrorDoctor.Desktop` | واجهة WPF (تعمل على ويندوز). |
| `src/ErrorDoctor.DataCollector` | أداة سطر أوامر تبني ملف التحديث (`error-manifest.json`) بدمج المجموعة الأولية مع مصادر عالمية موثوقة (Stack Overflow). |
| `tests/ErrorDoctor.Core.Tests` | اختبارات الوحدة + اختبار تكامل مقابل SQL Server حقيقي. |

البنية تفصل كل المنطق في `ErrorDoctor.Core` بحيث يكون قابلاً للاختبار بالكامل بدون واجهة رسومية.

---

## كيف يعمل التشخيص (How matching works)

1. يُحلَّل نص الخطأ لاستخراج: **رموز الأخطاء** (مثل `HTTP 500.30`، `CS1061`)، **أنواع الاستثناءات**
   (`InvalidOperationException`...)، والكلمات المفتاحية المهمة (مع تجاهل الكلمات الشائعة).
2. تُقيَّم كل أخطاء قاعدة البيانات بوزن: رمز الخطأ المطابق (الأعلى) ← نوع الاستثناء ← تطابق الكلمات.
3. تُعرض أفضل النتائج مع **نسبة تطابق** والسبب والحل ورابط المصدر.

كل ذلك يجري **محلياً وبدون إنترنت** على البيانات المخزَّنة في SQL Server.

---

## التشغيل على ويندوز + Visual Studio

### المتطلبات
- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server **LocalDB** (يُثبَّت تلقائياً مع Visual Studio؛ أو ثبّته من "SQL Server Express LocalDB").
- Visual Studio 2022 (اختياري — يمكن استخدام سطر الأوامر).

### الخطوات
```bash
git clone <repo-url>
cd AspNetCoreErrorDoctor
dotnet build
```
ثم من Visual Studio: افتح `AspNetCoreErrorDoctor.sln`، اجعل `ErrorDoctor.Desktop` هو
المشروع البادئ، واضغط **F5**.

أو من سطر الأوامر على ويندوز:
```bash
dotnet run --project src/ErrorDoctor.Desktop
```

عند أول تشغيل، يُنشئ التطبيق قاعدة البيانات تلقائياً ويملؤها بالمجموعة الأولية من الأخطاء.

> **إعداد قاعدة البيانات:** سلسلة الاتصال موجودة في
> [`src/ErrorDoctor.Desktop/appsettings.json`](src/ErrorDoctor.Desktop/appsettings.json).
> الافتراضي يستخدم `(localdb)\MSSQLLocalDB`. غيّرها إذا أردت استخدام SQL Server كامل.

---

## التحديث التلقائي (Auto-update)

عند توفّر الإنترنت، يتحقق التطبيق من ملف تحديث (JSON) على الرابط المضبوط في `appsettings.json`:

```json
"Update": {
  "ManifestUrl": "https://raw.githubusercontent.com/your-org/AspNetCoreErrorDoctor/main/dist/error-manifest.json",
  "IntervalDays": 1
}
```

- إذا لم يمرّ على آخر تحديث ناجح أكثر من `IntervalDays`، يجري تحديث تلقائي في الخلفية.
- إذا لم يتوفّر إنترنت، يستمر التطبيق بالعمل بالبيانات المحلية.
- يمكن للمستخدم الضغط على **تحديث قاعدة البيانات** يدوياً في أي وقت.

> غيّر `ManifestUrl` ليشير إلى ملفك المنشور على GitHub (أو أي خادم موثوق).

---

## بناء ملف التحديث (Data collector)

تبني الأداة ملف `error-manifest.json` بدمج المجموعة الأولية مع Stack Overflow:

```bash
# المجموعة الأولية فقط (بدون إنترنت):
dotnet run --project src/ErrorDoctor.DataCollector -- --output dist/error-manifest.json

# الدمج مع Stack Overflow (أعلى الأسئلة وسماً asp.net-core مع إجابة مقبولة):
dotnet run --project src/ErrorDoctor.DataCollector -- \
    --output dist/error-manifest.json --stackoverflow --max 200
```

انشر الملف الناتج (مثلاً ضمن مستودع GitHub) واجعل `ManifestUrl` يشير إليه. يمكن جدولة هذه
الأداة (GitHub Actions / Task Scheduler) لتحديث القاعدة يومياً أو أسبوعياً.

> المصادر قابلة للتوسعة: أضِف صنفاً يطبّق `ISource` (مثل GitHub Issues أو Microsoft Learn) وسجّله في `Program.cs`.

---

## الاختبارات (Tests)

```bash
# اختبارات الوحدة (لا تحتاج قاعدة بيانات):
dotnet test

# لتشغيل اختبار التكامل مع SQL Server أيضاً، اضبط سلسلة الاتصال:
export ERRORDOCTOR_SQL="Server=localhost,1433;Database=ErrorDoctorCI;User Id=sa;Password=YourPass;TrustServerCertificate=True;Encrypt=False"
dotnet test
```

---

## ملاحظات

- "كل أخطاء العالم" هدف يتحقق تدريجياً: التطبيق يبدأ بمجموعة أولية منسّقة عالية الجودة، وتكبر القاعدة
  مع كل تحديث من المصادر العالمية.
- WPF يعمل على **ويندوز فقط**؛ لكن كامل المنطق في `ErrorDoctor.Core` مُختبَر ويعمل على أي منصة.
