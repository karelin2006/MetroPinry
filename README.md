# MetroPinry 📌

A native, unofficial Windows Phone 8.1 client for [Pinry](https://github.com/pinry/pinry), built with C# and XAML. Designed with a strict focus on the authentic Metro UI design language and optimized for the hardware constraints of retro Windows Phone devices.

## Features

- **Full Authentication Support:** Login via standard credentials (automatically handles registration), API tokens, or browse anonymously in Guest mode.
- **Pin & Board Management:** Complete CRUD (Create, Read, Update, Delete) operations available via custom fast-response context menus.
- **Global Search:** Search through pins and boards directly from the app.
- **Authentic Metro Experience:** Adheres strictly to WP8.1 design guidelines (ApplicationBar, LongListSelector, typography, and dynamic placeholders).
- **Performance Optimized:** Implements memory-efficient image loading (`DecodePixelWidth`) to prevent `OutOfMemoryException` crashes on low-RAM devices while maintaining high-resolution imagery.
- **Custom Gestures:** Overrides system defaults to provide a responsive 300ms "Hold" gesture for context menus.

## Screenshots

<table>
  <tr>
    <td align="center">
      <img width="230" alt="wp_ss_20261101_0001" src="https://github.com/user-attachments/assets/1ef216e3-5c2a-43df-a10d-79e4db6ec135" />
    </td>
    <td align="center">
      <img width="230" alt="wp_ss_20261101_0002" src="https://github.com/user-attachments/assets/f87ba541-ff0d-4a1d-b769-ded97fa5a483" />
    </td>
    <td align="center">
      <img width="230" alt="wp_ss_20261101_0004" src="https://github.com/user-attachments/assets/6a00fffe-a80c-4802-8701-eae74adab963" />
    </td>
  </tr>
  <tr>
    <td align="center">
      <img width="230" alt="wp_ss_20261101_0003" src="https://github.com/user-attachments/assets/6eeb9f4e-b622-45ec-9277-b50345b6ff6c" />
    </td>
    <td align="center">
      <img width="230" alt="wp_ss_20261101_0006" src="https://github.com/user-attachments/assets/56549236-48d6-47bb-814a-cf0baeab4d09" />
    </td>
    <td></td> <!-- Пустая ячейка для симметрии сетки -->
  </tr>
</table>

## Requirements

- A Windows Phone 8.1 device (developer-unlocked) or Windows Phone 8.1 Emulator.
- A running instance of the Pinry server (accessible via HTTP/HTTPS).
- Visual Studio 2015 (if building from source).

## Installation (Sideloading)

Since the Windows Phone Store is closed, you will need to sideload the application:

1. Download the latest `.xap` or `.appx` package from the [Releases](../../releases) tab.
2. Ensure your Windows Phone is developer-unlocked.
3. Connect your phone to your PC via USB.
4. Deploy the app using the **Windows Phone Application Deployment Tool** (included in the WP8.1 SDK) or an alternative tool like **WPV XAP Deployer**.

## Built With

- **C# / XAML** (Silverlight 8.1)
- **Newtonsoft.Json** - For API payload serialization.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
