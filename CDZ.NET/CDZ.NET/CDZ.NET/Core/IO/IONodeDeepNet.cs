﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDZNET.Core
{
    [Serializable]
    public class IONodeDeepNet:IONode
    {
        protected List<IONode> layers;
        public List<IONode> Layers { get { return new List<IONode>(layers); } }
        public IONodeDeepNet()
            : base(new Point2D(0,0), new Point2D(0,0))
        {
            layers = new List<IONode>();
        }

        public void pushLayer(IONode n)
        {
            if (layers.Count != 0 && layers.Last().output.Size != n.input.Size)
                throw new Exception("The input size of the layer you are trying to push is not consistent with the output of the previous layer.");

            layers.Add(n);

            if (layers.Count == 1)
                Resize(n.input.Size, n.output.Size); //resize both input and output
            else
                Resize(input.Size, n.output.Size); //resize only the output
        }

        protected override void bottomUp()
        {
            //Copy the input of the network to the first layer
            Array.Copy(input.reality, layers.First().input.reality, input.reality.Length);

            for (int i = 0; i < layers.Count; i++)
            {
                layers[i].BottomUp();

                //Copy output to the next layer's input
                if (layers[i] != layers.Last())
                    Array.Copy(layers[i].output.prediction, layers[i + 1].input.reality, layers[i].output.prediction.Length);
            }

            //Copy the output of the last layer to the output of the network
            Array.Copy(layers.Last().output.prediction, output.prediction, layers.Last().output.prediction.Length);
        }

        protected override void topDown()
        {            
            //Copy the output of the network to the output of the last layer
            Array.Copy(output.prediction, layers.Last().output.reality, output.reality.Length);

            for (int i = layers.Count-1; i >=0 ; i--)
            {
                layers[i].TopDown();

                //Copy input to the previous layer's output
                if (layers[i] != layers.First())
                    Array.Copy(layers[i].input.prediction, layers[i - 1].output.reality, layers[i].input.prediction.Length);
            }

            //Copy the input of the first layer to the input of the network
            Array.Copy(layers.First().input.prediction, input.prediction, input.prediction.Length);
        }

    }
}
