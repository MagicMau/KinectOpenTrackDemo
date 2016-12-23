using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.FaceTracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectOpenTrack
{
    class SkeletonFaceTracker : IDisposable
    {
        internal Vector3DF rotation;
        internal Vector3DF translation;

        private FaceTracker faceTracker;

        private bool lastFaceTrackSucceeded;

        private SkeletonTrackingState skeletonTrackingState;

        public int LastTrackedFrame { get; set; }

        public void Dispose()
        {
            if (faceTracker != null)
            {
                faceTracker.Dispose();
                faceTracker = null;
            }
        }

        /// <summary>
        /// Updates the face tracking information for this skeleton
        /// </summary>
        internal void OnFrameReady(KinectSensor kinectSensor, ColorImageFormat colorImageFormat, byte[] colorImage, DepthImageFormat depthImageFormat, short[] depthImage, Skeleton skeletonOfInterest)
        {
            skeletonTrackingState = skeletonOfInterest.TrackingState;

            if (skeletonTrackingState != SkeletonTrackingState.Tracked)
            {
                // nothing to do with an untracked skeleton.
                return;
            }

            if (faceTracker == null)
            {
                try
                {
                    faceTracker = new FaceTracker(kinectSensor);
                }
                catch (InvalidOperationException)
                {
                    // During some shutdown scenarios the FaceTracker
                    // is unable to be instantiated.  Catch that exception
                    // and don't track a face.
                    System.Diagnostics.Trace.WriteLine("SkeletonFaceTracker - creating a new FaceTracker threw an InvalidOperationException");
                    faceTracker = null;
                }
            }

            if (faceTracker != null)
            {
                FaceTrackFrame frame = faceTracker.Track(
                    colorImageFormat, colorImage, depthImageFormat, depthImage, skeletonOfInterest);

                lastFaceTrackSucceeded = frame.TrackSuccessful;
                if (lastFaceTrackSucceeded)
                {
                    rotation = frame.Rotation;
                    translation = frame.Translation;
                }
            }
        }
    }
}
