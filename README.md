# ATS / ETS2 Native Telemetry App (FH6 v2.3)

<p align="center">
  <img src="screenshots/Captura de pantalla 2026-10-28 225007.png" alt="ATS / ETS2 Native Telemetry App Preview" width="100%">
</p>

Aplicación de escritorio independiente para **Windows (x64)** que lee y muestra la telemetría en tiempo real de **American Truck Simulator (ATS)** y **Euro Truck Simulator 2 (ETS2)** con una interfaz gráfica avanzada basada en el panel **FH6 v2.3** a 60 FPS mediante **SkiaSharp**.

---

## Características Principales

- **Independiente y Pasiva:** Aplicación nativa de Windows que se conecta de forma pasiva a la memoria compartida (`Local\SCSTelemetry`) del juego. No interfiere ni envía comandos al simulador.
- **Panel FH6 a 60 FPS:** Lienzo virtual escalable con velocímetro, odómetro, marcha, RPM, control de crucero y 13 testigos interactivos.
- **Logos Reales de Empresa:** Lee automáticamente el perfil activo (`profile.sii`) para mostrar el nombre y el logo real de empresa (`logo_0` a `logo_7`).
- **Clima Automático:** Detecta las condiciones climáticas (lluvia, tormenta, neblina, nublado) leyendo la humedad de la carretera desde la memoria del juego.
- **Lectura de Finanzas:** Lee saldos de cuentas bancarias y registros de beneficios de conductores directamente desde el archivo `game.sii` del perfil.
- **Bandeja de Sistema (System Tray):** Control total de modo ventana, pantalla, clima manual y cierre seguro desde la bandeja de Windows.

---

## Requisitos del Sistema

- **Sistema Operativo:** Windows 10 / 11 (64 bits).
- **Runtime:** .NET 8.0 (incluido o autocontenido en el paquete portable).
- **Juegos compatibles:** American Truck Simulator (ATS) y/o Euro Truck Simulator 2 (ETS2).

---

## Guía de Instalación

### 1. Instalación del Plugin de Telemetría
Para que la aplicación reciba datos de los juegos, es necesario instalar el plugin de telemetría de RenCloud (`scs-telemetry.dll`) en cada simulador:
1. Ejecuta el instalador automático ubicado en la raíz:
   ```cmd
   install-plugin.bat
   ```
   *(El script detecta automáticamente las rutas de Steam para ATS y ETS2).*
2. **Reinicia el juego** para que cargue el plugin.

### 2. Ejecución de la Aplicación
Puedes ejecutar la aplicación ya compilada desde la carpeta `dist`:
- **Modo Demo (sin necesidad de abrir el juego):**
  ```cmd
  dist\ATS-Telemetry-Windows\ATSTelemetry.exe --demo --windowed
  ```
- **Modo Normal (con el juego abierto):**
  ```cmd
  dist\ATS-Telemetry-Windows\ATSTelemetry.exe
  ```

### 3. Compilación desde Código Fuente (Opcional)
Si deseas compilar la aplicación por tu cuenta:
```powershell
# Compilar y publicar la versión Release
.\build.ps1
```

---

## Versiones y Lanzamientos

### Versión actual: `v2.3` (Octubre 2026)
- **Artefatos:** `dist\ATS-Telemetry-Windows.zip` (paquete portable x64).
- **Mejoras:** Soporte completo de logos de empresa reales, renderizado optimizado a 60 FPS sin fugas GDI+, lectura de clima por humedad y selector de pantalla/modo ventana desde la bandeja del sistema.

---

## Atajos y Controles Útiles

- **Cambiar de pantalla / modo:** Usar el menú flotante de la bandeja de sistema, o argumentos `--screen=DISPLAY2`, `--windowed`.
- **Pantalla completa / Ventana:** Tecla `F11`.
- **Cerrar aplicación:** `Ctrl + Shift + Q` o clic derecho en la bandeja -> **Salir**.
- **Archivo de Preferencias:** `%LOCALAPPDATA%\ATSTelemetry\window.ini`.

---

## Estructura del Proyecto

```text
Windows Native Final/
  build.ps1 / build.bat          Script de compilación y publicación x64
  install-plugin.ps1 / .bat      Instalador del plugin scs-telemetry.dll
  run.bat / run-demo.bat         Lanzadores rápidos
  Assets/
    Indicators/                  Testigos e iconos indicadores (PNG)
    BrandLogos/                  Logos de marcas de camiones (DAF, Volvo, Scania, etc.)
    CompanyLogos/                Logos reales de empresa (logo_0..logo_7)
    Plugins/scs-telemetry.dll    Plugin de telemetría SCS
  src/                           Código fuente en C# (.NET 8 + SkiaSharp + WinForms)
  dist/ATS-Telemetry-Windows/    Distribución portable compilada
  README.md                      Este documento
  GUIA-INTERFAZ.md               Guía técnica de la interfaz y coordenadas
  HISTORIAL.md                   Historial y bitácora de desarrollo
```

---

## Evolución y Capturas de Pantalla (Historial de Versiones)

Las capturas de pantalla guardadas en el repositorio documentan la evolución visual y técnica de la aplicación, cotejadas por su fecha y hora de creación:

1. **`Captura de pantalla 2026-10-26 190340.png`** *(26 Oct 2026 - 19:03)* -> **v2.0-alpha**: Estructura base inicial del lienzo SkiaSharp y pruebas de velocímetro.
2. **`Captura de pantalla 2026-10-26 204041.png`** *(26 Oct 2026 - 20:40)* -> **v2.0**: Incorporación de clúster de RPM, marcha y primeros testigos.
3. **`Captura de pantalla 2026-10-26 225454.png`** *(26 Oct 2026 - 22:54)* -> **v2.1**: Implementación de la cabecera de empresa y logotipos de marca.
4. **`Captura de pantalla 2026-10-27 234636.png`** *(27 Oct 2026 - 23:46)* -> **v2.2**: Adición de paneles de GPS, daños, clima y franja inferior de estado.
5. **`Captura de pantalla 2026-10-28 225007.png`** *(28 Oct 2026 - 22:50)* -> **v2.3 (Versión Actual / Estable)**: Versión final con soporte de logos reales de empresa (`logo_N.png`), optimización a 60 FPS y lectura pasiva MMF.

---

## Licencia y Créditos
- Desarrollado por **HS-Gaming**.
- Telemetría basada en `scs-telemetry.dll` de RenCloud.
- Interfaz gráfica inspirada en el estándar FH6.
