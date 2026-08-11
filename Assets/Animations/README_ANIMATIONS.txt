IMPORTANT: Setting up Animation Clips in Unity

The Player Animator Builder script looks for AnimationClip assets in the Animations folder.

If you're seeing "No animation clips found in the specified folder", here's what to check:

1. **FBX Import Settings**:
   - Select each .fbx file in the Animations folder
   - In the Inspector, go to the "Animation" tab
   - Make sure "Import Animation" is checked
   - Under "Animations", you should see the animation clips listed
   - Set "Animation Type" to "Humanoid" (or Generic if your rig isn't humanoid)
   - Click "Apply"

2. **Extracting Animation Clips**:
   - After setting the import settings, Unity should generate AnimationClip assets
   - These appear as child assets under each .fbx file in the Project window
   - They usually have the .anim extension or show as AnimationClip icons

3. **Alternative: Use the AnimationControllerGenerator**:
   - If you continue to have issues, try the "Tools/Generate Animation Controller" menu item
   - This creates a simpler controller that might work better for testing

4. **Manual Assignment**:
   - You can manually create an Animator Controller:
     1. Right-click in the Animations folder → Create → Animator Controller
     2. Name it "PlayerAnimator.controller"
     3. Double-click it to open in the Animator window
     4. Drag your animation clips from the Project window into the Animator window
     5. Set up states and transitions as needed

5. **Folder Structure**:
   - Make sure your animation files are actually in: Assets/Animations/
   - The script looks specifically in this folder

Once the AnimationClip assets are properly imported and visible in Unity, the Player Animator Builder should detect them and generate your controller automatically.