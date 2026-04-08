# OptiVis

OptiVis — Issabel PBX CDR ma'lumotlari uchun real-time call analytics platformasi.  
Loyiha .NET 10 va Avalonia asosida, backend + desktop + web arxitekturada ishlaydi.

## Asosiy imkoniyatlar

- Dashboard: jami qo'ng'iroqlar, trendlar, recent calls
- Operatorlar statistikasi va operator detail sahifasi
- Raqamlar (search) bo'yicha agregatsiya va detalizatsiya
- SignalR orqali real-time yangilanish
- Sozlamalarda backend URL boshqaruvi
- Noto'g'ri URL holatida startup fallback va xatolik logi

## Arxitektura

- **Backend**: Clean Architecture (`Domain`, `Application`, `Infrastructure`, `API`)
- **Frontend**: Avalonia (`UI.Shared`), hostlar: `Desktop` va `Web`
- **Realtime**: `CdrPollingWorker` + `SignalR Hub`
- **Data access policy**: DB'ga yozish emas, asosan o'qish (read-focused analytics)

## Solution tarkibi

```text
src/
  backend/
    OptiVis.Domain
    OptiVis.Application
    OptiVis.Infrastructure
    OptiVis.API
  frontend/
    OptiVis.UI.Shared
    OptiVis.Desktop
    OptiVis.Web
```

## Texnologiyalar

- .NET 10
- ASP.NET Core Web API
- MediatR
- EF Core
- SignalR
- Serilog
- Avalonia UI
- LiveCharts

## Ishga tushirish

### 1. Backend

`src\backend\OptiVis.API\appsettings.json` ichida connection string va polling intervalni tekshiring.

```powershell
cd src\backend\OptiVis.API
dotnet run
```

Backend default: `http://localhost:5000`

Foydali endpointlar:
- `GET /` (health)
- `GET /openapi/v1.json`
- Swagger UI
- SignalR hub: `/hubs/dashboard`

### 2. Desktop

```powershell
cd src\frontend\OptiVis.Desktop
dotnet run
```

Desktop sozlamalari `%AppData%\OptiVis\settings.json` da saqlanadi.

### 3. Web (ixtiyoriy)

Web variant uchun wasm workload kerak:

```powershell
dotnet workload restore
cd src\frontend\OptiVis.Web
dotnet run
```

## URL validatsiya va startup himoya

Sozlamalarda noto'g'ri backend URL saqlanib qolsa:

- App yiqilib qolmaydi
- URL avtomatik default qiymatga qaytariladi
- Foydalanuvchiga ogohlantirish ko'rsatiladi
- Startup xatolari `%AppData%\OptiVis\startup-errors.log` fayliga yoziladi

## Build

```powershell
dotnet build OptiVis.slnx
```

## Docker

API uchun Dockerfile mavjud:

```text
src\backend\OptiVis.API\Dockerfile
```

## Litsenziya

`LICENSE.txt` fayliga qarang.
