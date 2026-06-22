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
| `src/ErrorDoctor.DataCollector` | أداة سطر أوامر (اختيارية) تبني ملف التحديث (`error-manifest.json`) بدمج المجموعة الأولية مع المصادر الموثوقة (Stack Overflow + GitHub). |
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

## التحديث من المنصات الموثوقة (Live update)

عند الضغط على زر **تحديث قاعدة البيانات** (أو تلقائياً كل `IntervalDays`)، يجلب التطبيق
الأخطاء والحلول **مباشرةً من منصات موثوقة على الإنترنت** ويدمجها في قاعدة بياناتك المحلية —
بدون الحاجة إلى استضافة ملف تحديث:

- **Stack Overflow** (Stack Exchange API): أعلى الأسئلة تصويتاً بإجابة مقبولة، وسم `asp.net-core`.
- **GitHub**: قضايا مُغلقة عالية التفاعل من المستودعات الرسمية `dotnet/aspnetcore` و`dotnet/runtime`.

الضبط في [`appsettings.json`](src/ErrorDoctor.Desktop/appsettings.json):

```json
"Update": {
  "ManifestUrl": "",
  "IntervalDays": 1,
  "Sources": {
    "StackOverflow": true,
    "GitHub": true,
    "Tag": "asp.net-core",
    "MaxStackOverflowQuestions": 100,
    "StackAppsKey": "",
    "GitHubToken": ""
  }
}
```

- يدمج التطبيق الجديد ويحدّث المتغيّر فقط (مطابقة بالـ `ExternalId` وبصمة المحتوى).
- إذا لم يتوفّر إنترنت (تعذّر الوصول لكل المصادر)، يستمر التطبيق بالبيانات المحلية ويعرض رسالة بذلك.
- `StackAppsKey` و`GitHubToken` اختياريان لرفع حدود معدّل الطلبات فقط؛ ليسا مطلوبين.
- `ManifestUrl` اختياري: لو ضبطته على ملف منشور، يُدمج كمصدر إضافي إلى جانب المنصات الحيّة.

---

## بناء ملف تحديث منشور (Data collector — اختياري)

التطبيق يجلب من المنصات مباشرةً، لكن يمكنك أيضاً بناء ملف `error-manifest.json` ثابت ونشره
(مثلاً لشبكات بدون وصول مباشر إلى تلك المنصات):

```bash
# المجموعة الأولية فقط (بدون إنترنت):
dotnet run --project src/ErrorDoctor.DataCollector -- --output dist/error-manifest.json

# الدمج مع Stack Overflow وGitHub:
dotnet run --project src/ErrorDoctor.DataCollector -- \
    --output dist/error-manifest.json --stackoverflow --github --max 200
```

انشر الملف الناتج واجعل `ManifestUrl` يشير إليه. يمكن جدولة هذه الأداة (GitHub Actions / Task Scheduler).

> المصادر موحّدة في `ErrorDoctor.Core/Sync/Sources`. لإضافة منصّة جديدة: أضِف صنفاً يطبّق
> `IErrorSource` (مثل Microsoft Learn) وسجّله في `AggregateManifestSource` و`DataCollector/Program.cs`.

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
