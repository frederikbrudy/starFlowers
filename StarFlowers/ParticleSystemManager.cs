using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Media3D;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace Particles
{
    class ParticleSystemManager
    {
        private Dictionary<System.Windows.Media.Color, ParticleSystem> particleSystems;

        public ParticleSystemManager()
        {
            this.particleSystems = new Dictionary<System.Windows.Media.Color, ParticleSystem>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsed">the elapsed time since the last frame. something close to 0.016</param>
        public void Update(float elapsed)
        {
            foreach (ParticleSystem ps in this.particleSystems.Values)
            {
                ps.Update(elapsed);
            }
        }

        /// <summary>
        /// creates a new particle system at the desired startig position. 
        /// </summary>
        /// <param name="position"></param>
        /// <param name="speed"></param>
        /// <param name="color"></param>
        /// <param name="size"></param>
        /// <param name="life"></param>
        public void SpawnParticle(Point3D position, double speed, System.Windows.Media.Color color, double size, double life)
        {
            try
            {
                ParticleSystem ps = this.particleSystems[color];
                ps.SpawnParticle(position, speed, size, life);
            }
            catch { }
        }

        public void SpawnParticle(Point position, double speed, System.Windows.Media.Color color, double size, double life)
        {
            this.SpawnParticle(new Point3D(position.X, position.Y, 0.0), speed, color, size, life);
        }

        public Model3D CreateParticleSystem(int maxCount, System.Windows.Media.Color color)
        {
            ParticleSystem ps = new ParticleSystem(maxCount, color);
            this.particleSystems.Add(color, ps);
            return ps.ParticleModel;
        }

        public int ActiveParticleCount
        {
            get
            {
                int count = 0;
                foreach (ParticleSystem ps in this.particleSystems.Values)
                    count += ps.Count;
                return count;
            }
        }
    }
}
