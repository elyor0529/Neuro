﻿using System;
using System.Diagnostics;
using Neuro.Tensors;

namespace Neuro.Layers
{
    // https://www.youtube.com/watch?v=8oOgPUO-TBY
    public class Pooling : LayerBase
    {
        public Pooling(LayerBase prevLayer, int filterSize, int stride = 1, Tensor.PoolType type = Tensor.PoolType.Max)
            : this(prevLayer.OutputShape, filterSize, stride, type)
        {
        }

        public Pooling(Shape inputShape, int filterSize, int stride = 1, Tensor.PoolType type = Tensor.PoolType.Max)
            : base(inputShape,
                   new Shape((int)Math.Floor((float)(inputShape.Width - filterSize) / stride + 1),
                             (int)Math.Floor((float)(inputShape.Height - filterSize) / stride + 1),
                             inputShape.Depth))
        {
            Type = type;
            FilterSize = filterSize;
            Stride = stride;
        }

        public override LayerBase Clone()
        {
            return new Pooling(InputShape, FilterSize, Stride, Type);
        }

        protected override void FeedForwardInternal()
        {
            Input.Pool(FilterSize, Stride, Type, Tensor.PaddingType.Valid, Output);

            if (NeuralNetwork.DebugMode)
                Trace.WriteLine($"Pool(t={Type},f={FilterSize},s={Stride}) output:\n{Output}\n");
        }

        protected override void BackPropInternal(Tensor outputGradient)
        {
            Tensor.PoolGradient(Output, Input, outputGradient, FilterSize, Stride, Type, Tensor.PaddingType.Valid, InputGradient);

            if (NeuralNetwork.DebugMode)
                Trace.WriteLine($"Pool(t={Type},f={FilterSize},s={Stride}) errors gradient:\n{InputGradient}\n");
        }

        private readonly Tensor.PoolType Type;
        private readonly int FilterSize;
        private readonly int Stride;
    }
}
