using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;


namespace StarFlowers
{
    class Plant
    {
        private Image img;

        private bool isWithering = false;

        private bool isGrowing = true;

        private Color color;

        private int spriteIndex;
        private DateTime grownCompletedTime;

        private int plantId;

        //private int aliveSeconds;


        public Plant(Image img, int spriteIndex)
        {
            this.img = img; 
            this.spriteIndex = spriteIndex;
        }

        public int PlantId
        {
            get
            {
                return this.plantId;
            }
            set
            {
                this.plantId = value;
            }
        }

        public int SpriteIndex
        {
            get
            {
                return this.spriteIndex;
            }
        }

        public int TimeGrown
        {
            get
            {
                return (DateTime.Now - this.grownCompletedTime).Seconds;
            }
        }

        public bool IsGrowing
        {
            get
            {
                return this.isGrowing;
            }
            set
            {
                this.isGrowing = value;
                if (this.isGrowing)
                {
                    //ensure that the plant is not withering AND growing at the same time
                    this.isWithering = false;
                }
                else
                {
                    this.grownCompletedTime = DateTime.Now;
                }
            }
        }

        public bool IsWithering
        {
            get
            {
                return this.isWithering;
            }
            set
            {
                this.isWithering = value;
                if (this.isWithering)
                {
                    //ensure that the plant is not withering AND growing at the same time
                    this.isGrowing = false;
                }
            }
        }

        public Image Img
        {
            get
            {
                return this.img;
            }
            set
            {
                this.img = value;
            }
        }

    }
}
