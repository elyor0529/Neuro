﻿using Neuro.Layers;
using Neuro.Tensors;
using Neuro.Optimizers;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Neuro.PerfTests
{
    class SimpleNetPerfTests
    {
        static void Main(string[] args)
        {
            var inputs = new Tensor(new float[] { 1,1,2,2,3,3,4,4,5,5,6,6 }, new Shape(1, 2, 1, 6));
            var outputs = new Tensor(new float[] { 2,2,3,3,4,4,5,5,6,6,7,7 }, new Shape(1, 2, 1, 6));

            var net = new NeuralNetwork("test");
            net.AddLayer(new Dense(2, 5, Activation.Sigmoid));
            net.AddLayer(new Dense(net.LastLayer, 4, Activation.Sigmoid));
            net.AddLayer(new Dense(net.LastLayer, 2, Activation.Linear));
            net.Optimize(new Adam(0.01f), Loss.MeanSquareError);

            var l0 = net.Layer(0) as Dense;
            l0.Weights = new Tensor(new[] {-0.5790837f ,  0.79525125f, -0.6933877f , -0.3692013f ,  0.1810553f,
                                            0.03039712f,  0.91264546f,  0.11529088f,  0.33134186f, -0.46221718f }, new Shape(l0.Weights.Height, l0.Weights.Width)).Transposed();

            var l1 = net.Layer(1) as Dense;
            l1.Weights = new Tensor(new[] { 0.08085728f, -0.10262775f,  0.38443696f, -0.23273587f,
                                            0.33498216f, -0.7566199f , -0.814561f  , -0.08565235f,
                                           -0.55490625f,  0.6140275f ,  0.34785295f, -0.3431782f,
                                            0.47427893f, -0.41688982f,  0.59143007f,  0.00616223f,
                                            0.60304165f,  0.6548513f , -0.78456855f,  0.4640578f }, new Shape(l1.Weights.Height, l1.Weights.Width)).Transposed();

            var l2 = net.Layer(2) as Dense;
            l2.Weights = new Tensor(new[] { 0.32492328f,  0.6930735f,
                                           -0.7263415f ,  0.4574399f,
                                            0.5422747f ,  0.19008946f,
                                            0.911242f  , -0.24971604f }, new Shape(l2.Weights.Height, l2.Weights.Width)).Transposed();

            Trace.WriteLine(net.Predict(inputs.GetBatch(0)));

            net.Fit(inputs, outputs, 1, 10, 2, Track.Nothing, false);

            /*var inShape = new Shape(20);
            var outShape = new Shape(20);

            List<Data> trainingData = new List<Data>();

            for (int i = 0; i < 500; ++i)
            {
                var input = new Tensor(inShape);
                input.FillWithRand(3 * i);
                var output = new Tensor(outShape);
                output.FillWithRand(3 * i);
                trainingData.Add(new Data() { Input = input, Output = output });
            }

            var net = new NeuralNetwork("simple_net_perf_test");
            net.AddLayer(new Flatten(inShape));
            net.AddLayer(new Dense(net.LastLayer, 24, Activation.ReLU));
            net.AddLayer(new Dense(net.LastLayer, 24, Activation.ReLU));
            net.AddLayer(new Dense(net.LastLayer, outShape.Length, Activation.Linear));
            net.Optimize(new Adam(), Loss.MeanSquareError);

            var timer = new Stopwatch();
            timer.Start();

            net.Fit(trainingData, 1, 100, null, 0, Track.Nothing);

            timer.Stop();
            Console.WriteLine($"{Math.Round(timer.ElapsedMilliseconds / 1000.0, 2)} seconds");*/

            return;
        }
    }
}