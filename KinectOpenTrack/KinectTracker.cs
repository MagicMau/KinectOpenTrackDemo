using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectOpenTrack
{
    public class KinectTracker : IDisposable
    {
        private KinectSensor sensor;

        private const uint MaxMissedFrames = 100;

        private readonly Dictionary<int, SkeletonFaceTracker> trackedSkeletons = new Dictionary<int, SkeletonFaceTracker>();

        private byte[] colorImage;

        private ColorImageFormat colorImageFormat = ColorImageFormat.Undefined;

        private short[] depthImage;

        private DepthImageFormat depthImageFormat = DepthImageFormat.Undefined;

        private bool disposed;

        private Skeleton[] skeletonData;

        private OpenTrackInterface opentrack;

        public KinectTracker(OpenTrackInterface opentrack)
        {
            this.opentrack = opentrack;
        }

        ~KinectTracker()
        {
            Dispose(false);
        }

        public bool Start()
        {
            foreach (var sensor in KinectSensor.KinectSensors)
            {
                if (sensor.Status == KinectStatus.Connected)
                {
                    this.sensor = sensor;
                    break; // use the first connected sensor
                }
            }

            if (sensor == null)
                return false;

            sensor.SkeletonStream.Enable();
            sensor.SkeletonStream.EnableTrackingInNearRange = true;
            sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
            sensor.AllFramesReady += Sensor_AllFramesReady;

            try
            {
                sensor.Start();
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine($"Error initializing Kinect: {e.Message}");
                return false;
            }

            return true;
        }

        public void Stop()
        {
            if (sensor != null)
                sensor.Stop();
        }

        public void TiltUp()
        {
            Tilt(5);
        }

        public void TiltDown()
        {
            Tilt(-5);
        }

        private void Tilt(int degrees)
        {
            // +/- 27 is the range
            int angle = Math.Max(-27, Math.Min(27, sensor.ElevationAngle + degrees));
            sensor.ElevationAngle = angle;
        }

        private void Sensor_AllFramesReady(object sender, AllFramesReadyEventArgs allFramesReadyEventArgs)
        {
            ColorImageFrame colorImageFrame = null;
            DepthImageFrame depthImageFrame = null;
            SkeletonFrame skeletonFrame = null;

            try
            {
                colorImageFrame = allFramesReadyEventArgs.OpenColorImageFrame();
                depthImageFrame = allFramesReadyEventArgs.OpenDepthImageFrame();
                skeletonFrame = allFramesReadyEventArgs.OpenSkeletonFrame();

                if (colorImageFrame == null || depthImageFrame == null || skeletonFrame == null)
                {
                    return;
                }

                // Check for image format changes.  The FaceTracker doesn't
                // deal with that so we need to reset.
                if (depthImageFormat != depthImageFrame.Format)
                {
                    ResetFaceTracking();
                    depthImage = null;
                    depthImageFormat = depthImageFrame.Format;
                }

                if (colorImageFormat != colorImageFrame.Format)
                {
                    ResetFaceTracking();
                    colorImage = null;
                    colorImageFormat = colorImageFrame.Format;
                }

                // Create any buffers to store copies of the data we work with
                if (depthImage == null)
                {
                    depthImage = new short[depthImageFrame.PixelDataLength];
                }

                // Get the skeleton information
                if (skeletonData == null || skeletonData.Length != skeletonFrame.SkeletonArrayLength)
                {
                    skeletonData = new Skeleton[skeletonFrame.SkeletonArrayLength];
                }

                colorImage = colorImageFrame.GetRawPixelData(); // wouldn't this be faster than copying over the pixels?
                depthImageFrame.CopyPixelDataTo(depthImage);
                skeletonFrame.CopySkeletonDataTo(skeletonData);
                

                // Update the list of trackers and the trackers with the current frame information
                foreach (Skeleton skeleton in skeletonData)
                {
                    if (skeleton.TrackingState == SkeletonTrackingState.Tracked
                        || skeleton.TrackingState == SkeletonTrackingState.PositionOnly)
                    {
                        // We want keep a record of any skeleton, tracked or untracked.
                        if (!trackedSkeletons.ContainsKey(skeleton.TrackingId))
                        {
                            trackedSkeletons.Add(skeleton.TrackingId, new SkeletonFaceTracker());
                        }

                        // Give each face tracker the updated frame.
                        SkeletonFaceTracker skeletonFaceTracker;
                        if (trackedSkeletons.TryGetValue(skeleton.TrackingId, out skeletonFaceTracker))
                        {
                            skeletonFaceTracker.OnFrameReady(sensor, colorImageFormat, colorImage, depthImageFormat, depthImage, skeleton);
                            skeletonFaceTracker.LastTrackedFrame = skeletonFrame.FrameNumber;

                            // now we have all the information we need and can update the joystick
                            float x = skeleton.Position.X;
                            float y = skeleton.Position.Y;
                            float z = skeleton.Position.Z;
                            float pitch = skeletonFaceTracker.rotation.Y;
                            float roll = skeletonFaceTracker.rotation.X;
                            float yaw = skeletonFaceTracker.rotation.Z;
                            opentrack.Update(x, y, z, pitch, roll, yaw);
                        }

                        break; // only process the first available skeleton
                    }
                }

                RemoveOldTrackers(skeletonFrame.FrameNumber);
            }
            finally
            {
                if (colorImageFrame != null)
                {
                    colorImageFrame.Dispose();
                }

                if (depthImageFrame != null)
                {
                    depthImageFrame.Dispose();
                }

                if (skeletonFrame != null)
                {
                    skeletonFrame.Dispose();
                }
            }
        }

        private void ResetFaceTracking()
        {
            foreach (int trackingId in new List<int>(trackedSkeletons.Keys))
            {
                RemoveTracker(trackingId);
            }
        }

        private void RemoveTracker(int trackingId)
        {
            trackedSkeletons[trackingId].Dispose();
            trackedSkeletons.Remove(trackingId);
        }

        /// <summary>
        /// Clear out any trackers for skeletons we haven't heard from for a while
        /// </summary>
        private void RemoveOldTrackers(int currentFrameNumber)
        {
            List<int> trackersToRemove = null;

            foreach (var tracker in trackedSkeletons)
            {
                int missedFrames = currentFrameNumber - tracker.Value.LastTrackedFrame;
                if (missedFrames > MaxMissedFrames)
                {
                    if (trackersToRemove == null)
                        trackersToRemove = new List<int>();

                    // There have been too many frames since we last saw this skeleton
                    trackersToRemove.Add(tracker.Key);
                }
            }

            foreach (int trackingId in trackersToRemove)
            {
                RemoveTracker(trackingId);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                ResetFaceTracking();

                disposed = true;
            }
        }
    }
}
