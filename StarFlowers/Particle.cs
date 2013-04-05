using System.Windows.Media.Media3D;

namespace Particles
{
    public class Particle
    {
        /// <summary>
        /// current position of the particle. reference to this object so it can be manipulated from outside
        /// </summary>
        public Point3D Position;

        /// <summary>
        /// the speed of position-change of the particle in 3D space. 
        /// </summary>
        public Vector3D Velocity;

        /// <summary>
        /// initial life count. bigger than 1. affects the size of the particle.
        /// </summary>
        public double StartLife;

        /// <summary>
        /// current life count. when smaller or equals to 0 -> means that this particle is dead and shall be removed. affects the size of the particle.
        /// </summary>
        public double Life;

        /// <summary>
        /// decay rate. defaults to 1. bigger than 1 means faster decay rate. between 0 and 1 means slower decay rate.
        /// </summary>
        public double Decay;

        /// <summary>
        /// initial size of this particles. decreases over time. the actula size can be found in Size
        /// </summary>
        public double StartSize;

        /// <summary>
        /// size of the particle 
        /// </summary>
        public double Size;
    }
}
