using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectOpenTrack
{
    public class OpenTrackInterface
    {

        public void Update(float x, float y, float z, float pitch, float roll, float yaw)
        {
            Console.WriteLine($"\rx = {x}, y = {y}, z = {z}, pitch = {pitch}, roll = {roll}, yaw = {yaw}");
        }
    }
}
