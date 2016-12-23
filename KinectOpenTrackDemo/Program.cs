using KinectOpenTrack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectOpenTrackDemo
{
    class Program
    {
        static void Main(string[] args)
        {

            var opentrack = new OpenTrackInterface();
            var kinect = new KinectTracker(opentrack);

            kinect.Start();

            Console.ReadLine();

            kinect.Stop();
        }
    }
}
