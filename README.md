# CS399

Clone the Repository
  Open a terminal and run:
    - git clone https://github.com/Bejte/CS399.git
    - cd CS399
  Open the Project in Webots
   - Launch Webots
   - Go to: File → Open World…
   - Select: worlds/city.wbt
   - (If not selected already) select the controller for the car to be "autonomous_vehicle" in webots interface.

FOR THE UNITY SIMULATION:
1. Open with Unity Hub

- Launch Unity Hub
- Click "Open"
- Select the cloned project folder (the one containing Assets/, ProjectSettings/, etc.)
- If Unity asks to upgrade or install a specific version, make sure to install the correct version via Unity Hub

2. Run the Scene

- In the Unity Editor, open the main scene (usually under Assets/Scenes/)
- Press the "Play" button at the top to run the simulation or game

3. Optional: Installing Dependencies

- If the project uses packages or third-party assets:
  - Go to "Window" → "Package Manager"
  - Click "Refresh" or install any missing packages if prompted

Notes:
- If you see pink materials or broken scripts:
  - Check that you're using the correct Unity version
  - Ensure all required packages are installed
- The Library/ folder is excluded via .gitignore. Unity will automatically regenerate it the first time you open the project (this may take several minutes).
