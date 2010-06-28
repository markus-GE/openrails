﻿/* LOCOMOTIVE CLASSES
 * 
 * Used a a base for Steam, Diesel and Electric locomotive classes.
 * 
 * A locomotive is represented by two classes:
 *  LocomotiveSimulator - defines the behaviour, ie physics, motion, power generated etc
 *  LocomotiveViewer - defines the appearance in a 3D viewer including animation for wipers etc
 *  
 * Both these classes derive from corresponding classes for a basic TrainCar
 *  TrainCarSimulator - provides for movement, rolling friction, etc
 *  TrainCarViewer - provides basic animation for running gear, wipers, etc
 *  
 * Locomotives can either be controlled by a player, 
 * or controlled by the train's MU signals for brake and throttle etc.
 * The player controlled loco generates the MU signals which pass along to every
 * unit in the train.
 * For AI trains, the AI software directly generates the MU signals - there is no
 * player controlled train.
 * 
 * The end result of the physics calculations for the the locomotive is
 * a TractiveForce and a FrictionForce ( generated by the TrainCar class )
 * 
 */
/// COPYRIGHT 2009 by the Open Rails project.
/// This code is provided to enable you to contribute improvements to the open rails program.  
/// Use of the code for any other purpose or distribution of the code to anyone else
/// is prohibited without specific written permission from admin@openrails.org.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MSTS;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using System.IO;



namespace ORTS
{

    ///////////////////////////////////////////////////
    ///   SIMULATION BEHAVIOUR
    ///////////////////////////////////////////////////


    /// <summary>
    /// Adds Throttle, Direction, Horn, Sander and Wiper control
    /// to the basic TrainCar.
    /// Use as a base for Electric, Diesel or Steam locomotives.
    /// </summary>
    public class MSTSLocomotive: MSTSWagon
    {
        // simulation parameters
        public bool Horn = false;
        public bool Bell = false;
        public bool Sander = false;  
        public bool Wiper = false;
        public bool BailOff = false;
        float MaxPowerW;
        float MaxForceN;
        float MaxSpeedMpS = 1e3f;
        public float MainResPressurePSI = 130;
        public bool CompressorOn = false;

        // wag file data
        public string CabSoundFileName = null;
        public string CVFFileName = null;
        public float MaxMainResPressurePSI = 130;
        public float MainResVolumeFT3 = 10;
        public float CompressorRestartPressurePSI = 110;

        public CVFFile CVFFile = null;

        public MSTSEngineController ThrottleController;
        public MSTSEngineController TrainBrakeController;
        public MSTSEngineController EngineBrakeController;

        public MSTSLocomotive(string  wagPath)
            : base(wagPath)
        {
            //Console.WriteLine("loco {0} {1} {2}", MaxPowerW, MaxForceN, MaxSpeedMpS);
        }

        /// <summary>
        /// This initializer is called when we haven't loaded this type of car before
        /// and must read it new from the wag file.
        /// </summary>
        public override void InitializeFromWagFile(string wagFilePath)
        {
            TrainBrakeController = new MSTSEngineController();
            EngineBrakeController = new MSTSEngineController();
            base.InitializeFromWagFile(wagFilePath);

            if (CVFFileName != null)
            {
                string CVFFilePath = Path.GetDirectoryName(WagFilePath) + @"\CABVIEW\" + CVFFileName;
                CVFFile = new CVFFile(CVFFilePath);

                // Set up camera locations for the cab views
                for( int i = 0; i < CVFFile.Locations.Count; ++i )
                {
                    if (i >= CVFFile.Locations.Count || i >= CVFFile.Directions.Count)
                    {
                        Console.Error.WriteLine("Position or Direction missing in " + CVFFilePath);
                        break;
                    }
                    ViewPoint viewPoint = new ViewPoint();
                    viewPoint.Location = CVFFile.Locations[i];
                    viewPoint.StartDirection = CVFFile.Directions[i];
                    viewPoint.RotationLimit = new Vector3( 0,0,0 );  // cab views have a fixed head position
                    FrontCabViewpoints.Add(viewPoint);
                }
            }

            IsDriveable = true;
            if (TrainBrakeController.StepSize == 0)
                TrainBrakeController = null;
            if (EngineBrakeController.StepSize == 0)
                EngineBrakeController = null;
        }

        /// <summary>
        /// Parse the wag file parameters required for the simulator and viewer classes
        /// </summary>
        public override void Parse(string lowercasetoken, STFReader f)
        {
            if (lowercasetoken.StartsWith("engine(trainbrakescontroller"))
                TrainBrakeController.ParseBrakeValue(lowercasetoken.Substring(28), f);
            if (lowercasetoken.StartsWith("engine(enginebrakescontroller"))
                TrainBrakeController.ParseBrakeValue(lowercasetoken.Substring(29), f);
            switch (lowercasetoken)
            {
                case "engine(sound": CabSoundFileName = f.ReadStringBlock(); break;
                case "engine(cabview": CVFFileName = f.ReadStringBlock(); break;
                case "engine(maxpower": MaxPowerW = ParseW(f.ReadStringBlock(),f); break;
                case "engine(maxforce": MaxForceN = ParseN(f.ReadStringBlock(),f); break;
                case "engine(maxvelocity": MaxSpeedMpS = ParseMpS(f.ReadStringBlock(),f); break;
                case "engine(enginecontrollers(throttle": ThrottleController = new MSTSEngineController(f); break;
                case "engine(enginecontrollers(regulator": ThrottleController = new MSTSEngineController(f); break;
                case "engine(enginecontrollers(brake_train": TrainBrakeController.Parse(f); break;
                case "engine(enginecontrollers(brake_engine": EngineBrakeController.Parse(f); break;
                case "engine(airbrakesmainresvolume": MainResVolumeFT3 = f.ReadFloatBlock(); break;
                case "engine(airbrakesmainmaxairpressure": MaxMainResPressurePSI = f.ReadFloatBlock(); break;
                case "engine(airbrakescompressorrestartpressure": CompressorRestartPressurePSI = f.ReadFloatBlock(); break;
                default: base.Parse(lowercasetoken, f); break;
            }
        }

        /// <summary>
        /// This initializer is called when we are making a new copy of a car already
        /// loaded in memory.  We use this one to speed up loading by eliminating the
        /// need to parse the wag file multiple times.
        /// </summary>
        public override void InitializeFromCopy(MSTSWagon copy)
        {
            MSTSLocomotive locoCopy = (MSTSLocomotive)copy;
            CabSoundFileName = locoCopy.CabSoundFileName;
            CVFFileName = locoCopy.CVFFileName;
            CVFFile = locoCopy.CVFFile;
            MaxPowerW = locoCopy.MaxPowerW;
            MaxForceN = locoCopy.MaxForceN;
            MaxSpeedMpS = locoCopy.MaxSpeedMpS;

            IsDriveable = copy.IsDriveable;
            ThrottleController = MSTSEngineController.Copy(locoCopy.ThrottleController);
            TrainBrakeController = MSTSEngineController.Copy(locoCopy.TrainBrakeController);
            EngineBrakeController = MSTSEngineController.Copy(locoCopy.EngineBrakeController);

            base.InitializeFromCopy(copy);  // each derived level initializes its own variables
        }

        /// <summary>
        /// We are saving the game.  Save anything that we'll need to restore the 
        /// status later.
        /// </summary>
        public override void Save(BinaryWriter outf)
        {
            // we won't save the horn state
            outf.Write(Bell);
            outf.Write(Sander);
            outf.Write(Wiper);
            outf.Write(MaxPowerW);
            outf.Write(MaxForceN);
            outf.Write(MaxSpeedMpS);
            outf.Write(MainResPressurePSI);
            outf.Write(CompressorOn);
            MSTSEngineController.Save(ThrottleController, outf);
            MSTSEngineController.Save(TrainBrakeController, outf);
            MSTSEngineController.Save(EngineBrakeController, outf);
            base.Save(outf);
        }

        /// <summary>
        /// We are restoring a saved game.  The TrainCar class has already
        /// been initialized.   Restore the game state.
        /// </summary>
        public override void Restore(BinaryReader inf)
        {
            if (inf.ReadBoolean()) SignalEvent(EventID.BellOn);
            if (inf.ReadBoolean()) SignalEvent(EventID.SanderOn);
            if (inf.ReadBoolean()) SignalEvent(EventID.WiperOn);
            MaxPowerW = inf.ReadSingle();
            MaxForceN = inf.ReadSingle();
            MaxSpeedMpS = inf.ReadSingle();
            MainResPressurePSI = inf.ReadSingle();
            CompressorOn = inf.ReadBoolean();
            ThrottleController = MSTSEngineController.Restore(inf);
            TrainBrakeController = MSTSEngineController.Restore(inf);
            EngineBrakeController = MSTSEngineController.Restore(inf);
            base.Restore(inf);
        }



        /// <summary>
        /// Create a viewer for this locomotive.   Viewers are only attached
        /// while the locomotive is in viewing range.
        /// </summary>
        public override TrainCarViewer GetViewer(Viewer3D viewer)
        {
            return new MSTSLocomotiveViewer(viewer, this);
        }

        /// <summary>
        /// This is a periodic update to calculate physics 
        /// parameters and update the base class's MotiveForceN 
        /// and FrictionForceN values based on throttle settings
        /// etc for the locomotive.
        /// </summary>
        public override void Update(float elapsedClockSeconds)
        {
            // TODO  this is a wild simplification for electric and diesel electric
            float t = ThrottlePercent / 100f;
            float maxForceN = MaxForceN * t;
            float maxPowerW = MaxPowerW * t * t;
            float maxSpeedMpS = MaxSpeedMpS * t;
            float currentSpeedMpS = Math.Abs(SpeedMpS);
            if (maxForceN * currentSpeedMpS > maxPowerW)
                maxForceN = maxPowerW / currentSpeedMpS;
            float balanceRatio = 1;
            if (maxSpeedMpS > currentSpeedMpS)
                balanceRatio = currentSpeedMpS / maxSpeedMpS;

            MotiveForceN = ( Direction == Direction.Forward ? 1 : -1) * maxForceN * (1f - balanceRatio);

            // Variable1 is wheel rotation in m/sec for steam locomotives
            Variable2 = Math.Abs(MotiveForceN) / MaxForceN;   // force generated
            Variable1 = ThrottlePercent / 100f;   // throttle setting

            if (MainResPressurePSI < CompressorRestartPressurePSI)
                CompressorOn = true;
            else if (MainResPressurePSI > MaxMainResPressurePSI)
                CompressorOn = false;
            if (CompressorOn)
                MainResPressurePSI += elapsedClockSeconds * .5f * Program.BrakePipeChargingRatePSIpS * .5f / MainResVolumeFT3;

            base.Update(elapsedClockSeconds);
        }

        public void SetDirection( Direction direction )
        {
            // Direction Control
            if ( Direction != direction && ThrottlePercent < 1)
            {
                Direction = direction;
                if (direction == Direction.Forward)
                {
                    SignalEvent(EventID.Forward);
                    Train.MUReverserPercent = 100;
                }
                else
                {
                    SignalEvent(EventID.Reverse);
                    Train.MUReverserPercent = -100;
                }
            }
        }

        public void IncreaseThrottle()
        {
            if (ThrottleController == null)
            {
                ThrottlePercent += 10;
                if (ThrottlePercent > 100)
                    ThrottlePercent = 100;
            }
            else
            {
                ThrottlePercent = ThrottleController.Increase() * 100;
            }
        }

        public void DecreaseThrottle()
        {
            if (ThrottleController == null)
            {
                ThrottlePercent -= 10;
                if (ThrottlePercent < 0)
                    ThrottlePercent = 0;
            }
            else
            {
                ThrottlePercent = ThrottleController.Decrease() * 100;
            }
        }
        public void ChangeTrainBrakes(float percent)
        {
            if (TrainBrakeController == null)
            {
                Train.AITrainBrakePercent += percent;
                if (Train.AITrainBrakePercent < 0) Train.AITrainBrakePercent = 0;
                if (Train.AITrainBrakePercent > 100) Train.AITrainBrakePercent = 100;
            }
            else if (percent > 0)
                TrainBrakeController.Increase();
            else
                TrainBrakeController.Decrease();
        }
        public void SetEmergency()
        {
            if (TrainBrakeController == null)
                Train.AITrainBrakePercent = 100;
            else
                TrainBrakeController.SetEmergency();
        }
        public override string GetTrainBrakeStatus()
        {
            if (TrainBrakeController == null)
                return BrakeSystem.GetStatus(1);
            string s = TrainBrakeController.GetStatus();
            if (BrakeSystem.GetType() == typeof(AirSinglePipe))
                s += string.Format(" EQ {0:F0} ", Train.BrakeLine1PressurePSI);
            else
                s += string.Format(" {0:F0} ", Train.BrakeLine1PressurePSI);
            s += BrakeSystem.GetStatus(1);
            TrainCar lastCar = Train.Cars[Train.Cars.Count - 1];
            if (lastCar == this)
                lastCar = Train.Cars[0];
            if (lastCar != this)
                s = s + " " + lastCar.BrakeSystem.GetStatus(0);
            return s;
        }
        public void ChangeEngineBrakes(float percent)
        {
            if (EngineBrakeController == null)
                return;
            if (percent > 0)
                EngineBrakeController.Increase();
            else
                EngineBrakeController.Decrease();
        }
        public override string GetEngineBrakeStatus()
        {
            if (EngineBrakeController == null)
                return null;
            return string.Format("{0}{1}", EngineBrakeController.GetStatus(), BailOff ? " BailOff" : "");
        }
        public void ToggleBailOff()
        {
            BailOff = !BailOff;
        }
        
        /// <summary>
        /// Used when someone want to notify us of an event
        /// </summary>
        public override void SignalEvent(EventID eventID)
        {
            switch (eventID)
            {
                case EventID.BellOn: Bell = true; break;
                case EventID.BellOff: Bell = false; break;
                case EventID.HornOn: Horn = true; break;
                case EventID.HornOff: Horn = false; break;
                case EventID.SanderOn: Sander = true; break;
                case EventID.SanderOff: Sander = false; break;
                case EventID.WiperOn: Wiper = true; break;
                case EventID.WiperOff: Wiper = false; break;
                case EventID.HeadlightOff: Headlight = 0; break;
                case EventID.HeadlightDim: Headlight = 1; break;
                case EventID.HeadlightOn:  Headlight = 2; break;
            }

            base.SignalEvent(eventID );
        }

    } // LocomotiveSimulator

    ///////////////////////////////////////////////////
    ///   3D VIEW
    ///////////////////////////////////////////////////

    /// <summary>
    /// Adds animation for wipers to the basic TrainCar
    /// </summary>
    public class MSTSLocomotiveViewer : MSTSWagonViewer
    {
        MSTSLocomotive Locomotive;

        List<int> WiperPartIndexes = new List<int>();

        float WiperAnimationKey = 0;

        protected MSTSLocomotive MSTSLocomotive { get { return (MSTSLocomotive)Car; } }

        public MSTSLocomotiveViewer(Viewer3D viewer, MSTSLocomotive car)
            : base(viewer, car)
        {
            Locomotive = car;


            // Find the animated parts
            if (TrainCarShape.SharedShape.Animations != null)
            {
                for (int iMatrix = 0; iMatrix < TrainCarShape.SharedShape.MatrixNames.Length; ++iMatrix)
                {
                    string matrixName = TrainCarShape.SharedShape.MatrixNames[iMatrix].ToUpper();
                    switch (matrixName)
                    {
                        case "WIPERARMLEFT1":
                        case "WIPERBLADELEFT1":
                        case "WIPERARMRIGHT1":
                        case "WIPERBLADERIGHT1":
                            if (TrainCarShape.SharedShape.Animations[0].FrameCount > 1)  // ensure shape file is properly animated for wipers
                                WiperPartIndexes.Add(iMatrix);
                            break;
                        case "MIRRORARMLEFT1":
                        case "MIRRORLEFT1":
                        case "MIRRORARMRIGHT1":
                        case "MIRRORRIGHT1":
                            // TODO
                            break;
                    }
                }
            }

            string wagonFolderSlash = Path.GetDirectoryName(Locomotive.WagFilePath) + "\\";
            if (Locomotive.CabSoundFileName != null) LoadCarSound(wagonFolderSlash, Locomotive.CabSoundFileName);

        }

        /// <summary>
        /// A keyboard or mouse click has occurred. Read the UserInput
        /// structure to determine what was pressed.
        /// </summary>
        public override void HandleUserInput(ElapsedTime elapsedTime)
        {
            if (UserInput.IsPressed(Keys.W)) Locomotive.SetDirection(Direction.Forward);
            if (UserInput.IsPressed(Keys.S)) Locomotive.SetDirection(Direction.Reverse);
            if (UserInput.IsPressed(Keys.D)) Locomotive.IncreaseThrottle();
            if (UserInput.IsPressed(Keys.A)) Locomotive.DecreaseThrottle();
            if (UserInput.IsPressed(Keys.OemQuotes) && !UserInput.IsShiftDown()) Locomotive.ChangeTrainBrakes(10);
            if (UserInput.IsPressed(Keys.OemSemicolon) && !UserInput.IsShiftDown()) Locomotive.ChangeTrainBrakes(-10);
            if (UserInput.IsPressed(Keys.OemOpenBrackets) && !UserInput.IsShiftDown()) Locomotive.ChangeEngineBrakes(-10);
            if (UserInput.IsPressed(Keys.OemCloseBrackets) && !UserInput.IsShiftDown()) Locomotive.ChangeEngineBrakes(10);
            if (UserInput.IsPressed(Keys.OemQuestion) && !UserInput.IsShiftDown()) Locomotive.ToggleBailOff();
            if (UserInput.IsPressed(Keys.OemQuestion) && UserInput.IsShiftDown()) Locomotive.Train.InitializeBrakes();
            if (UserInput.IsPressed(Keys.OemSemicolon) && UserInput.IsShiftDown()) Locomotive.Train.SetHandbrakePercent(0);
            if (UserInput.IsPressed(Keys.OemQuotes) && UserInput.IsShiftDown()) Locomotive.Train.SetHandbrakePercent(100);
            if (UserInput.IsPressed(Keys.OemOpenBrackets) && UserInput.IsShiftDown()) Locomotive.Train.SetRetainers(false);
            if (UserInput.IsPressed(Keys.OemCloseBrackets) && UserInput.IsShiftDown()) Locomotive.Train.SetRetainers(true);
            if (UserInput.IsPressed(Keys.OemPipe) && !UserInput.IsShiftDown()) Locomotive.Train.ConnectBrakeHoses();
            if (UserInput.IsPressed(Keys.OemPipe) && UserInput.IsShiftDown()) Locomotive.Train.DisconnectBrakes();
            if (UserInput.IsPressed(Keys.Back)) Locomotive.SetEmergency();
            if (UserInput.IsPressed(Keys.X)) Locomotive.Train.SignalEvent(Locomotive.Sander ? EventID.SanderOff : EventID.SanderOn); 
            if (UserInput.IsPressed(Keys.V)) Locomotive.SignalEvent(Locomotive.Wiper ? EventID.WiperOff : EventID.WiperOn);
            if (UserInput.IsKeyDown(Keys.Space) != Locomotive.Horn) Locomotive.SignalEvent(Locomotive.Horn ? EventID.HornOff : EventID.HornOn);
            if (UserInput.IsPressed(Keys.B) != Locomotive.Bell) Locomotive.SignalEvent(Locomotive.Bell ? EventID.BellOff : EventID.BellOn);
            if (UserInput.IsPressed(Keys.H) && UserInput.IsShiftDown())
                switch ((Locomotive.Headlight))
                {
                    case 1: Locomotive.Headlight = 0; break;
                    case 2: Locomotive.Headlight = 1; break;
                }
            else if (UserInput.IsPressed(Keys.H))
                switch ((Locomotive.Headlight))
                {
                    case 0: Locomotive.Headlight = 1; break;
                    case 1: Locomotive.Headlight = 2; break;
                }

            base.HandleUserInput( elapsedTime );
        }

        /// <summary>
        /// We are about to display a video frame.  Calculate positions for 
        /// animated objects, and add their primitives to the RenderFrame list.
        /// </summary>
        public override void PrepareFrame(RenderFrame frame, ElapsedTime elapsedTime)
        {
            float elapsedClockSeconds = elapsedTime.ClockSeconds;
            // Wiper animation
            if (WiperPartIndexes.Count > 0)  // skip this if there are no wipers
            {
                if (Locomotive.Wiper) // on
                {
                    // Wiper Animation
                    // Compute the animation key based on framerate etc
                    // ie, with 8 frames of animation, the key will advance from 0 to 8 at the specified speed.
                    WiperAnimationKey += ((float)TrainCarShape.SharedShape.Animations[0].FrameRate / 10f) * elapsedClockSeconds;
                    while (WiperAnimationKey >= TrainCarShape.SharedShape.Animations[0].FrameCount) WiperAnimationKey -= TrainCarShape.SharedShape.Animations[0].FrameCount;
                    while (WiperAnimationKey < -0.00001) WiperAnimationKey += TrainCarShape.SharedShape.Animations[0].FrameCount;
                    foreach (int iMatrix in WiperPartIndexes)
                        TrainCarShape.AnimateMatrix(iMatrix, WiperAnimationKey);
                }
                else // off
                {
                    if (WiperAnimationKey > 0.001)  // park the blades
                    {
                        WiperAnimationKey += ((float)TrainCarShape.SharedShape.Animations[0].FrameRate / 10f) * elapsedClockSeconds;
                        if (WiperAnimationKey >= TrainCarShape.SharedShape.Animations[0].FrameCount) WiperAnimationKey = 0;
                        foreach (int iMatrix in WiperPartIndexes)
                            TrainCarShape.AnimateMatrix(iMatrix, WiperAnimationKey);
                    }
                }
            }

            base.PrepareFrame( frame, elapsedTime );
        }


        /// <summary>
        /// This doesn't function yet.
        /// </summary>
        public override void Unload()
        {
            base.Unload();
        }

    } // Class LocomotiveViewer



}
