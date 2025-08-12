# Unity WebGL Build and Test Guide

This guide explains how to build a Unity project for WebGL and how to test it locally.

---

## Prerequisites

- **Unity Editor** (version 2019.4 LTS or later recommended)
- A Unity project set up and ready to build
- A web browser (Chrome, Firefox, Edge, or Safari)

---

## How to Build the Unity Project for WebGL

1.  **Open your Unity Project**

2.  **Switch Platform to WebGL**
    - Go to `File` > `Build Settings...`
    - Select **WebGL** from the list of platforms.
    - Click **Switch Platform**.

3.  **Configure Player Settings (Optional)**
    - In the Build Settings window, click **Player Settings...**
    - Adjust resolution, WebGL template, compression format, and other settings as needed.

4.  **Build the Project**
    - In the Build Settings window, click **Build**.
    - Choose a folder to save the WebGL build (e.g., `Build/WebGL`).
    - Wait for Unity to finish the build process.

---

## How to Test the WebGL Build Locally

Because WebGL builds use JavaScript and browser security policies, opening the `index.html` file directly from your file system usually won’t work properly. You need to serve the files via a local web server.

### Option 1: Using Unity's Built-in Web Server (Editor)

- After building, Unity offers an option to **Build and Run** which opens the build in your default browser via a local server.

### Option 2: Using a Simple Local HTTP Server

If you already built the project:

- **Using Python (if installed)**  
  Navigate to the folder containing your WebGL build `index.html`, then run:

  ```bash
  # For Python 3.x
  python -m http.server 8000
  ```
### Using Node.js http-server
If you have Node.js installed, install http-server globally:
 ```bash
npm install -g http-server
  ```
Then run:
```Bash
http-server -p 8000
```
Open your browser and go to http://localhost:8000 to see the WebGL build running.
                            
               

## Troubleshooting

### Build errors:

Make sure the WebGL module is installed in Unity Hub for your Unity version.
Blank screen or errors in browser:
Use the browser's developer console to check for errors.
### Performance issues:
Try disabling compression or adjusting quality settings in Player Settings.
## Additional Resources
[Unity Manual: Building and Running a WebGL Project
](https://docs.unity3d.com/Manual/webgl-building.html)

[Unity WebGL Best Practices](https://unity.com/webgl-best-practices)
