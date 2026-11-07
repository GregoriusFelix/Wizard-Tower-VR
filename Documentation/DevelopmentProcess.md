# a. Documentation of Development Process

The prototype is a mobile VR fantasy exploration game developed for the **video-game and interactive-entertainment industry**. Its intended audience is players seeking a short fantasy adventure using an Android phone and a Cardboard-compatible viewer. The player explores a wizard tower and its surroundings, gathers firewood, experiments with potions and completes a torch-lighting objective to unlock an elemental encounter with a stone golem. The gameplay loop is to explore, interact, complete an objective and choose whether to replay the encounter or continue exploring.

The prototype demonstrates how these adventure-game activities can be controlled through head-directed gaze without requiring separate hand controllers. Navigation pads connect the activities, instruction boards explain the objectives, and animation, sound and visual effects communicate the results of player actions. This makes its industry application specific: it is a proof of concept for a gaze-controlled mobile VR adventure, with exploration, resource collection, crafting-style interactions and a replayable encounter. These features provide a basis for developing a larger fantasy game with additional objectives and content; commercial performance and player satisfaction have not been evaluated.

Development used Unity 6, the Universal Render Pipeline and Google Cardboard XR Plugin v1.30.0. The outdoor area, tower floors and arena are contained within one scene.

The environment was constructed by combining Unity primitives and ProBuilder geometry. Cubes, cylinders and other permitted shapes were arranged and scaled to create buildings, furniture, trees, staffs and the golem. Color-only materials distinguish wood, stone, metal and magical elements. Decorative props were added around the paths and inside the tower. Signs provide directions and explain the axe-to-cauldron and torch-to-golem sequences. These additions follow the lecturer's clarified permission to use ProBuilder, built-in text, particles and audio.

**[Insert work-in-progress screenshot 1: Unity Scene view showing the tower, paths and primitive-built props.]**

*Figure 1. Work-in-progress environment layout and construction using primitives and ProBuilder.*

Gaze interaction was implemented using camera-forward raycasts and dwell timers. A center indicator and progress display show when a target is being selected. Completing the dwell time activates the corresponding interaction. Scripted travel and facing changes use a fade and move the player rig to preserve headset tracking. The main functions and their relevance to the adventure-game design are summarised below.

| Prototype function | Player action and resulting behaviour | Application in the game |
|---|---|---|
| Navigation | Gaze at teleport pads to travel; use portals to reach different tower floors. | Connects exploration areas and gives access to activities and objectives. |
| Axe chopping | Pick up the axe by gaze, approach the tree and gaze at its target ring for three strikes. The tree falls and produces logs. | Provides a resource-gathering task with a clear action sequence and completion result. |
| Log collection and cauldron fuel | Gaze at fallen logs to carry up to three, then gaze at the firewood beneath the cauldron to deliver them. The fire becomes stronger. | Links collecting a resource to a visible change in the game world. |
| Potion mixing | Gaze at ingredient vials to pour them into a receiving bottle and change the mixture's colour. | Demonstrates a crafting-style activity with feedback that responds to ingredient selection. |
| Object interaction | Gaze at door handles or the chest to open and close them; activate the chair to sit or stand. | Makes the environment respond to the player and supports exploration beyond the main objective. |
| Magical interaction | Activate objects such as the floating books, globe and orrery. | Reinforces the wizard setting through interactive props and animated responses. |
| Elemental encounter | Light both tomb cores, choose a staff, enter the arena and gaze at the golem to cast a spell. | Combines an unlocking objective, player choice and a combat encounter into a progression sequence. |

These functions provide more than five interaction types, with navigation supporting access to each activity.

The golem encounter connects several interactions into a progression sequence. The player collects a torch, lights it from a burning torch and ignites two tomb cores. Lightning then assembles the golem from a rock pile. After the introduction, the player chooses Water, Fire, Earth or Air and receives the corresponding staff and spellbook. Gazing at the golem casts the selected spell. Reset Golem allows another attempt, while Leave Arena clears the held equipment and returns the player to exploration.

**[Insert work-in-progress screenshot 2: the earlier staff-selection interface without panel backgrounds.]**

*Figure 2. Early staff selection before visible selection areas and fitted element labels were added.*

Audio clips were connected to actions such as chopping, knocking, pouring, sitting and spell casting. A central audio settings asset provides overall and individual volume controls. Animation timing was refined to match the three recorded door knocks and the chest's closing thud. Particles, emission and primitive-based effects provide visual feedback for fire, magic and impacts.

Development involved identifying problems from Unity previews and an initial Android build. The first APK displayed stereoscopic images but revealed missing camera tracking and a poorly visible center indicator. A separate player rig, rotation-only tracking and Cardboard startup handling were added. The mobile indicator was enlarged, and decoration lights were corrected from directional to local point lights. Other refinements included centering the outer staff labels, raising the arena buttons above the floor, and changing scattered stones to gravity-based falls with grounded resting poses. Editor and Android C# compilation checks passed; the latest changes still require final on-device verification.

**[Insert work-in-progress screenshot 3: the initial split-screen Android APK screenshot.]**

*Figure 3. Initial Android build used to identify tracking, indicator visibility and lighting issues.*

The resulting prototype applies its navigation and interaction system to a connected fantasy-game experience. The torch objective leads into the golem encounter, the four staff choices offer different spell effects, and the Reset Golem and Leave Arena controls support replay and a return to exploration. The following final-product screenshots should provide evidence of these functions after the final Play mode and phone checks.

**[Insert final-product screenshot 4: the outdoor environment, tower and direction sign.]**

*Figure 4. Final exploration environment, with teleport pads and signs guiding the player between the tower and golem route.*

**[Insert final-product screenshot 5: an action-and-result pair showing axe chopping and collected logs, or vial pouring and the resulting potion colour.]**

*Figure 5. A gaze-controlled activity and its visible result. Identify the exact action shown in the final caption.*

**[Insert final-product screenshot 6: the equipped staff and a visible spell effect directed at the golem. An additional image can show the defeated golem with Reset Golem and Leave Arena.]**

*Figure 6. Elemental spell casting in the golem encounter. If a second image is included, show the controls for replaying the encounter or returning to exploration.*

---

## Screenshot preparation notes (remove before submission)

- Screenshot placeholders have not been replaced with actual images. No project screenshots were found in the workspace, apart from Unity's tutorial icon.
- The environment, early staff-selection and initial Android screenshots already shared in the chat can be used as work-in-progress evidence.
- Capture new images of the latest working version for Figures 4–6. Avoid using older screenshots with floating stones, misaligned labels or a hidden button background as final-product evidence.
- Capture final images without transform gizmos or selection outlines. Keep text readable and use consistent image sizes in the submitted document.
- Complete phone testing before submission and update the verification sentence to describe what was actually tested. Do not claim that the latest APK passed device testing until it has been checked.
- Add the commercial app that inspired the prototype once it has been selected for the five-app comparison report.
- For the functionality and industry-relevance criterion, retain the named industry, intended audience, gameplay loop and feature-to-purpose comparison. Use screenshots that show what the player does and what changes afterwards, rather than environment-only images.
