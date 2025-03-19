using iFacialMocapTrackingModule;
using VRCFaceTracking;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Params.Expressions;

public class iFacialMocapTrackingInterface : ExtTrackingModule
{
    iFacialMocapServer server = new();
    // What your interface is able to send as tracking data.
    public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);

    // This is the first function ran by VRCFaceTracking. Make sure to completely initialize 
    // your tracking interface or the data to be accepted by VRCFaceTracking here. This will let 
    // VRCFaceTracking know what data is available to be sent from your tracking interface at initialization.
    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        ModuleInformation.Name = "iFacialMocap";

        // Example of an embedded image stream being referenced as a stream
        var stream = GetType().Assembly.GetManifestResourceStream("VRCFT_iFacialMocap.res.logo.png");


        // Setting the stream to be referenced by VRCFaceTracking.
        ModuleInformation.StaticImages =
            stream != null ? new List<Stream> { stream } : ModuleInformation.StaticImages;

        //... Initializing module. Modify state tuple as needed (or use bool contexts to determine what should be initialized).
        server.Connect(ref Logger);
        var state = (server.isTracking, server.isTracking);
        return state;
    }

    // Polls data from the tracking interface.
    // VRCFaceTracking will run this function in a separate thread;
    public override void Update()
    {
        // Get latest tracking data from interface and transform to VRCFaceTracking data.
        server.ReadData(ref Logger);
        if (server.isTracking)
        {
            if (Status == ModuleState.Active) // Module Status validation
            {
                // ... Execute update cycle.
                //UnifiedTracking.Data.Eye.Left.Openness = ExampleTracker.LeftEye.Openness;
                //UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.JawOpen] = ExampleTracker.Mouth.JawOpen;
                UpdateData();
            }
            // Add a delay or halt for the next update cycle for performance. eg: 
            Thread.Sleep(1);
        }
    }

    // Called when the module is unloaded or VRCFaceTracking itself tears down.
    public override void Teardown()
    {
        //... Deinitialize tracking interface; dispose any data created with the module.
        server.Stop();
    }

    void UpdateData()
    {
        //Could make a dict<UnifiedExpressions,string> or directly assigning Data.Shapes for better performance but can do math here so whatever for now.
        #region Eye Gaze
        // positive x is to the *right*
        //UnifiedTracking.Data.Eye.Left.Gaze.x = server.FaceData.BlendValue("eyeLookIn_L") - server.FaceData.BlendValue("eyeLookOut_L");
        //UnifiedTracking.Data.Eye.Left.Gaze.y = server.FaceData.BlendValue("eyeLookUp_L") - server.FaceData.BlendValue("eyeLookDown_L");
        //UnifiedTracking.Data.Eye.Right.Gaze.x = server.FaceData.BlendValue("eyeLookOut_R") - server.FaceData.BlendValue("eyeLookIn_R");
        //UnifiedTracking.Data.Eye.Right.Gaze.y = server.FaceData.BlendValue("eyeLookUp_R") - server.FaceData.BlendValue("eyeLookDown_R");

        // coordinate system is all wacky
        UnifiedTracking.Data.Eye.Left.Gaze.x = -MathF.Tan(server.FaceData.leftEye[1] / 90.0f); // normalized range of -45 to 45 degrees, tan function
        UnifiedTracking.Data.Eye.Left.Gaze.y = -MathF.Tan(server.FaceData.leftEye[0] / 90.0f);
        UnifiedTracking.Data.Eye.Right.Gaze.x = -MathF.Tan(server.FaceData.rightEye[1] / 90.0f);
        UnifiedTracking.Data.Eye.Right.Gaze.y = -MathF.Tan(server.FaceData.rightEye[0] / 90.0f);
        #endregion
        #region Eye Openness
        UnifiedTracking.Data.Eye.Left.Openness = 1.0f - (float)Math.Max(0, Math.Min(1, server.FaceData.BlendValue("eyeBlink_L") +
                server.FaceData.BlendValue("eyeBlink_L") * server.FaceData.BlendValue("eyeSquint_L")));

        UnifiedTracking.Data.Eye.Right.Openness = 1.0f - (float)Math.Max(0, Math.Min(1, server.FaceData.BlendValue("eyeBlink_R") +
                server.FaceData.BlendValue("eyeBlink_R") * server.FaceData.BlendValue("eyeSquint_R")));

        #endregion
        #region Eye Blends
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeSquintLeft].Weight = server.FaceData.BlendValue("eyeSquint_L");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeSquintRight].Weight = server.FaceData.BlendValue("eyeSquint_R");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeWideLeft].Weight = server.FaceData.BlendValue("eyeWide_L");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeWideRight].Weight = server.FaceData.BlendValue("eyeWide_R");
        #endregion
        #region Pupil
        //EyeDilation & EyeConstrict default in mid value idk
        UnifiedTracking.Data.Eye.Left.PupilDiameter_MM = 5f;
        UnifiedTracking.Data.Eye.Right.PupilDiameter_MM = 5f;
        UnifiedTracking.Data.Eye._minDilation = 0;
        UnifiedTracking.Data.Eye._maxDilation = 10;
        #endregion
        #region Eye Brow
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowInnerUpLeft].Weight = server.FaceData.BlendValue("browInnerUp_L");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowLowererLeft].Weight = server.FaceData.BlendValue("browDown_L");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowOuterUpLeft].Weight = server.FaceData.BlendValue("browOuterUp_L");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowPinchLeft].Weight = server.FaceData.BlendValue("browDown_L");

        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowInnerUpRight].Weight = server.FaceData.BlendValue("browInnerUp_R");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowLowererRight].Weight = server.FaceData.BlendValue("browDown_R");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowOuterUpRight].Weight = server.FaceData.BlendValue("browOuterUp_R");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowPinchRight].Weight = server.FaceData.BlendValue("browDown_R");
        #endregion 
        #region Nose
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.NoseSneerLeft].Weight = server.FaceData.BlendValue("noseSneer_L");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.NoseSneerRight].Weight = server.FaceData.BlendValue("noseSneer_R");
        //Default NasalDitalation & NasalConstrict
        #endregion
        #region Cheek
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.CheekPuffLeft].Weight = server.FaceData.BlendValue("cheekPuff");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.CheekSquintLeft].Weight = server.FaceData.BlendValue("cheekSquint_L");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.CheekPuffRight].Weight = server.FaceData.BlendValue("cheekPuff");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.CheekSquintRight].Weight = server.FaceData.BlendValue("cheekSquint_R");
        //No CheekSuck'ing lol
        #endregion
        #region Mewing
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.JawLeft].Weight = server.FaceData.BlendValue("jawLeft");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.JawRight].Weight = server.FaceData.BlendValue("jawRight");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.JawOpen].Weight = server.FaceData.BlendValue("jawOpen");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthClosed].Weight = server.FaceData.BlendValue("mouthClose");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.JawForward].Weight = server.FaceData.BlendValue("jawForward");
        //Default JawBackward, JawClench & JawMandibleRaise
        #endregion
        #region Lip 
        //lips expressions = mouth expressions
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.LipPuckerUpperLeft].Weight = server.FaceData.BlendValue("mouthPucker");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.LipPuckerLowerLeft].Weight = server.FaceData.BlendValue("mouthPucker");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.LipPuckerUpperRight].Weight = server.FaceData.BlendValue("mouthPucker");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.LipPuckerLowerRight].Weight = server.FaceData.BlendValue("mouthPucker");

        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.LipFunnelUpperLeft].Weight = server.FaceData.BlendValue("mouthFunnel");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.LipFunnelLowerLeft].Weight = server.FaceData.BlendValue("mouthFunnel");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.LipFunnelUpperRight].Weight = server.FaceData.BlendValue("mouthFunnel");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.LipFunnelLowerRight].Weight = server.FaceData.BlendValue("mouthFunnel");

        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = Math.Min(
            1f - (float)Math.Pow(server.FaceData.BlendValue("mouthUpperUp_L"), 1 / 6f),
            server.FaceData.BlendValue("mouthRollUpper")
        );
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = server.FaceData.BlendValue("mouthRollLower");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.LipSuckUpperRight].Weight = Math.Min(
            1f - (float)Math.Pow(server.FaceData.BlendValue("mouthUpperUp_L"), 1 / 6f),
            server.FaceData.BlendValue("mouthRollUpper")
        );
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.LipSuckLowerRight].Weight = server.FaceData.BlendValue("mouthRollLower");
        #endregion
        #region Mouth
        //not sure if appropiate ussage of shrug
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthRaiserLower].Weight = server.FaceData.BlendValue("mouthShrugLower");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthRaiserUpper].Weight = server.FaceData.BlendValue("mouthShrugUpper");

        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthUpperLeft].Weight = server.FaceData.BlendValue("mouthLeft");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthLowerLeft].Weight = server.FaceData.BlendValue("mouthLeft");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthUpperUpLeft].Weight = server.FaceData.BlendValue("mouthUpperUp_L");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthLowerDownLeft].Weight = server.FaceData.BlendValue("mouthLowerDown_L");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthCornerPullLeft].Weight = server.FaceData.BlendValue("mouthSmile_L");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthLowerDownLeft].Weight = server.FaceData.BlendValue("mouthLowerDown_L");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthDimpleLeft].Weight = server.FaceData.BlendValue("mouthDimple_L");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthFrownLeft].Weight = server.FaceData.BlendValue("mouthFrown_L");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthPressLeft].Weight = server.FaceData.BlendValue("mouthPress_L");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthStretchLeft].Weight = server.FaceData.BlendValue("mouthStretch_L");

        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthUpperRight].Weight = server.FaceData.BlendValue("mouthRight");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthLowerRight].Weight = server.FaceData.BlendValue("mouthRight");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthUpperUpRight].Weight = server.FaceData.BlendValue("mouthUpperUp_R");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthLowerDownRight].Weight = server.FaceData.BlendValue("mouthLowerDown_R");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthCornerPullRight].Weight = server.FaceData.BlendValue("mouthSmile_R");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthLowerDownRight].Weight = server.FaceData.BlendValue("mouthLowerDown_R");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthDimpleRight].Weight = server.FaceData.BlendValue("mouthDimple_R");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthFrownRight].Weight = server.FaceData.BlendValue("mouthFrown_R");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthPressRight].Weight = server.FaceData.BlendValue("mouthPress_R");
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.MouthStretchRight].Weight = server.FaceData.BlendValue("mouthStretch_R");

        //Default MouthUpperDeepenLeft, MouthCornerSlantLeft & MouthTightenerLeft
        #endregion
        #region Tongue
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.TongueOut].Weight = server.FaceData.BlendValue("tongueOut");
        //Not sure if any more
        #endregion
    }
}