﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Xml;
using Neuro.Tensors;

namespace Neuro
{
    [Flags]
    public enum Track
    {
        Nothing = 0,
        TrainError = 1 << 0,
        TestError = 1 << 1,
        TrainAccuracy = 1 << 2,
        TestAccuracy = 1 << 3,
        All = -1
    }
    
    // Best explanation I found are Neural Network series from Coding Train on YT
    public class NeuralNetwork
    {
        public NeuralNetwork(string name, int seed = 0)
        {
            Name = name;
            if (seed > 0)
            {
                Seed = seed;
                Tools.Rng = new Random(seed);
            }
        }

        public NeuralNetwork Clone()
        {
            var clone = new NeuralNetwork(Name, Seed);
            foreach (var layer in Layers)
                clone.Layers.Add(layer.Clone());
            clone.Optimize(Optimizer, Error);
            return clone;
        }

        public void CopyParametersTo(NeuralNetwork target)
        {
            for (int i = 0; i < Layers.Count; ++i)
                Layers[i].CopyParametersTo(target.Layers[i]);
        }

        public string Name;

        public string FilePrefix
        {
            get { return Name.ToLower().Replace(" ", "_"); }
        }

        public Layers.LayerBase Layer(int i)
        {
            return Layers[i];
        }

        public Layers.LayerBase LastLayer
        {
            get { return Layers.Last(); }
        }

        public int LayersCount
        {
            get { return Layers.Count; }
        }

        public void AddLayer(Layers.LayerBase layer)
        {
            layer.Init();
            Layers.Add(layer);
        }

        public Tensor Predict(Tensor input)
        {
            for (int l = 0; l < Layers.Count; l++)
                Layers[l].FeedForward(l == 0 ? input : Layers[l - 1].Output);

            return Layers.Last().Output.Clone();
        }

        private void FeedForward(Tensor input)
        {
            if (NeuralNetwork.DebugMode)
                Trace.WriteLine($"Input:\n{input}\n");

            for (int l = 0; l < Layers.Count; l++)
                Layers[l].FeedForward(l == 0 ? input : Layers[l - 1].Output);
        }

        private void BackProp(Tensor delta)
        {
            if (NeuralNetwork.DebugMode)
                Trace.WriteLine($"Errors gradient:\n{delta}\n");

            for (int l = Layers.Count - 1; l >= 0; --l)
                delta = Layers[l].BackProp(delta);
        }

        private void UpdateParameters(int trainingSamples)
        {
            for (int l = 0; l < Layers.Count; ++l)
                Layers[l].UpdateParameters(trainingSamples);
        }

        public void Optimize(Optimizers.OptimizerBase optimizer, LossFunc loss)
        {
            Error = loss;
            Optimizer = optimizer;

            for (int l = 0; l < Layers.Count; ++l)
                Layers[l].Optimizer = optimizer.Clone();
        }

        public void Fit(Tensor input, Tensor output, int batchSize = -1, int epochs = 1, int verbose = 1, Track trackFlags = Track.TrainError | Track.TestAccuracy, bool shuffle = true)
        {
            if (input.BatchSize != output.BatchSize) throw new Exception($"Mismatched input and output batch size.");

            List<Data> trainingData = new List<Data>();

            if (batchSize > 0 && batchSize != input.BatchSize)
            {
                // we have to split input and output tensors into datas so they can be shuffled later on
                for (int i = 0; i < input.BatchSize; ++i)
                    trainingData.Add(new Data() { Input = input.GetBatch(i), Output = output.GetBatch(i) });
            }
            else
                trainingData.Add(new Data() { Input = input, Output = output });

            Fit(trainingData, batchSize, epochs, null, verbose, trackFlags, shuffle);
        }

        public void Fit(List<Tensor> inputs, List<Tensor> outputs, int batchSize = -1, int epochs = 1, int verbose = 1, Track trackFlags = Track.TrainError | Track.TestAccuracy, bool shuffle = true)
        {
            if (inputs.Count != outputs.Count) throw new Exception($"Mismatched number of inputs and outputs.");

            List<Data> trainingData = new List<Data>();
            for (int i = 0; i < inputs.Count; ++i)
            {
                if (inputs[i].BatchSize != 1 && inputs.Count > 1) throw new Exception($"Input tensor at index {i} has multiple batches in it, this is not supported!");
                if (outputs[i].BatchSize != 1 && outputs.Count > 1) throw new Exception($"Output tensor at index {i} has multiple batches in it, this is not supported!");
                trainingData.Add(new Data() { Input = inputs[i], Output = outputs[i] });
            }

            Fit(trainingData, batchSize, epochs, null, verbose, trackFlags, shuffle);
        }

        // Training method, when batch size is -1 the whole training set is used for single gradient descent step (in other words, batch size equals to training set size)
        public void Fit(List<Data> trainingData, int batchSize = -1, int epochs = 1, List<Data> validationData = null, int verbose = 1, Track trackFlags = Track.TrainError | Track.TestAccuracy, bool shuffle = true)
        {
            LogLines.Clear();

            bool trainingDataAlreadyBatched = trainingData[0].Input.BatchSize > 1;

            for (int i = 0; i < trainingData.Count; ++i)
            {
                Data d = trainingData[i];
                Debug.Assert(d.Input.BatchSize == d.Output.BatchSize, $"Training data set contains mismatched number if input and output batches for data at index {i}!");
                Debug.Assert(d.Input.BatchSize == trainingData[0].Input.BatchSize, "Training data set contains batches of different size!");
            }

            if (batchSize < 0)
                batchSize = trainingDataAlreadyBatched ? trainingData[0].Input.BatchSize : trainingData.Count;

            string outFilename = $"{FilePrefix}_training_data_{Optimizer.GetType().Name.ToLower()}_b{batchSize}{(Seed > 0 ? ("_seed" + Seed) : "")}_{Tensor.CurrentOpMode}";
            ChartGenerator chartGen = null;
            if (trackFlags != Track.Nothing)
                chartGen = new ChartGenerator($"{outFilename}", $"{Name} [{Error.GetType().Name}, {Optimizer}, BatchSize={batchSize}]\nSeed={(Seed > 0 ? Seed.ToString() : "None")}, TensorMode={Tensor.CurrentOpMode}", "Epoch");

            if (trackFlags.HasFlag(Track.TrainError))
                chartGen.AddSeries((int)Track.TrainError, "Error on train data\n(left Y axis)", Color.DarkRed);
            if (trackFlags.HasFlag(Track.TestError))
                chartGen.AddSeries((int)Track.TestError, "Error on test data\n(left Y axis)", Color.IndianRed);
            if (trackFlags.HasFlag(Track.TrainAccuracy))
                chartGen.AddSeries((int)Track.TrainAccuracy, "Accuracy on train data\n(right Y axis)", Color.DarkBlue, true);
            if (trackFlags.HasFlag(Track.TestAccuracy))
                chartGen.AddSeries((int)Track.TestAccuracy, "Accuracy on test\n(right Y axis)", Color.CornflowerBlue, true);

            var lastLayer = Layers.Last();
            int outputsNum = lastLayer.OutputShape.Length;

            int batchesNum = trainingDataAlreadyBatched ? trainingData.Count : (trainingData.Count / batchSize);
            int totalTrainingSamples = trainingData.Count * trainingData[0].Input.BatchSize;

            AccuracyFunc accuracyFunc = Tools.AccNone;

            if (trackFlags.HasFlag(Track.TrainAccuracy) || trackFlags.HasFlag(Track.TestAccuracy))
            {
                if (outputsNum == 1)
                    accuracyFunc = Tools.AccBinaryClassificationEquality;
                else
                    accuracyFunc = Tools.AccCategoricalClassificationEquality;
            }

            Stopwatch trainTimer = new Stopwatch();

            for (int e = 1; e <= epochs; ++e)
            {
                string output;

                if (verbose > 0)
                    LogLine($"Epoch {e}/{epochs}");

                // no point shuffling stuff when we have single batch
                if (batchesNum > 1 && shuffle)
                    trainingData.Shuffle();

                List<Data> batchedTrainingData = trainingDataAlreadyBatched ? trainingData : Tools.MergeData(trainingData, batchSize);

                float trainTotalError = 0;
                int trainHits = 0;

                trainTimer.Restart();

                for (int b = 0; b < batchedTrainingData.Count; ++b)
                {
                    // this will be equal to batch size; however, the last batch size may be different if there is a reminder of training data by batch size division
                    int samples = batchedTrainingData[b].Input.BatchSize;
                    GradientDescentStep(batchedTrainingData[b], samples, accuracyFunc, ref trainTotalError, ref trainHits);

                    if (verbose == 2)
                    {
                        output = Tools.GetProgressString(b * batchSize + samples, totalTrainingSamples);
                        Console.Write(output);
                        Console.Write(new string('\b', output.Length));
                    }
                }

                trainTimer.Stop();

                output = Tools.GetProgressString(totalTrainingSamples, totalTrainingSamples);

                if (verbose > 0)
                    LogLine(output);

                float trainError = trainTotalError / totalTrainingSamples;

                chartGen?.AddData(e, trainError, (int)Track.TrainError);
                chartGen?.AddData(e, (float)trainHits / totalTrainingSamples, (int)Track.TrainAccuracy);

                if (verbose > 0)
                {
                    string s = $" - loss: {Math.Round(trainError, 6)}";
                    if (trackFlags.HasFlag(Track.TrainAccuracy))
                        s += $" - acc: {Math.Round((float)trainHits / totalTrainingSamples * 100, 4)}%";
                    s += " - eta: " + trainTimer.Elapsed.ToString(@"mm\:ss\.ffff");

                    LogLine(s);
                }

                float testTotalError = 0;

                if (validationData != null)
                {
                    int validationSamples = validationData.Count * validationData[0].Input.BatchSize;
                    float testHits = 0;

                    for (int i = 0; i < validationData.Count; ++i)
                    {
                        FeedForward(validationData[i].Input);
                        Tensor loss = new Tensor(lastLayer.Output.Shape);
                        Error.Compute(validationData[i].Output, lastLayer.Output, loss);
                        testTotalError += loss.Sum() / outputsNum;
                        testHits += accuracyFunc(validationData[i].Output, lastLayer.Output);

                        if (verbose == 2)
                        {
                            string progress = " - validating: " + Math.Round(i / (float)validationData.Count * 100) + "%";
                            Console.Write(progress);
                            Console.Write(new string('\b', progress.Length));
                        }
                    }

                    chartGen?.AddData(e, testTotalError / (validationSamples * lastLayer.OutputShape.Length), (int)Track.TestError);
                    chartGen?.AddData(e, (float)testHits / validationSamples, (int)Track.TestAccuracy);
                }

                if (e % 20 == 0 || e == epochs)
                    chartGen?.Save();
            }

            File.WriteAllLines($"{outFilename}_log.txt", LogLines);
        }

        // This is vectorized gradient descent
        private void GradientDescentStep(Data trainingData, int samplesInTrainingData, AccuracyFunc accuracyFunc, ref float trainError, ref int trainHits)
        {
            var lastLayer = Layers.Last();

            FeedForward(trainingData.Input);
            Tensor loss = new Tensor(lastLayer.Output.Shape);
            Error.Compute(trainingData.Output, lastLayer.Output, loss);
            trainError += loss.Sum() / lastLayer.OutputShape.Length;
            trainHits += accuracyFunc(trainingData.Output, lastLayer.Output);
            Error.Derivative(trainingData.Output, lastLayer.Output, loss);
            BackProp(loss);
            UpdateParameters(samplesInTrainingData);
        }

        private void LogLine(string text)
        {
            LogLines.Add(text);
            Console.WriteLine(text);
        }

        public string Summary()
        {
            int totalParams = 0;
            string output = "_________________________________________________________________\n";
            output += "Layer Type                   Output Shape              Param #\n";
            output += "=================================================================\n";

            foreach (var layer in Layers)
            {
                totalParams += layer.GetParamsNum();
                output += $"{layer.GetType().Name.PadRight(29)}"+ $"({layer.OutputShape.Width}, {layer.OutputShape.Height}, {layer.OutputShape.Depth})".PadRight(26) + $"{layer.GetParamsNum()}\n";
                output += "_________________________________________________________________\n";
            }

            output += $"Total params: {totalParams}";

            return output;
        }

        public void SaveStateXml(string filename = "")
        {
            XmlDocument doc = new XmlDocument();
            XmlElement modelElem = doc.CreateElement("Sequential");

            for (int l = 0; l < Layers.Count; l++)
            {
                XmlElement layerElem = doc.CreateElement(Layers[l].GetType().Name);
                Layers[l].SerializeParameters(layerElem);
                modelElem.AppendChild(layerElem);
            }

            doc.AppendChild(modelElem);
            doc.Save(filename.Length == 0 ? $"{FilePrefix}.xml" : filename);
        }

        public void LoadStateXml(string filename = "")
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(filename.Length == 0 ? $"{FilePrefix}.xml" : filename);
            XmlElement modelElem = doc.FirstChild as XmlElement;

            for (int l = 0; l < Layers.Count; l++)
            {
                XmlElement layerElem = modelElem.ChildNodes.Item(l) as XmlElement;
                Layers[l].DeserializeParameters(layerElem);
            }
        }

        public static bool DebugMode = false;
        private List<Layers.LayerBase> Layers = new List<Layers.LayerBase>();
        private LossFunc Error = Loss.MeanSquareError;
        private Optimizers.OptimizerBase Optimizer;
        private int Seed;
        private delegate int AccuracyFunc(Tensor targetOutput, Tensor output);
        private List<string> LogLines = new List<string>();
    }
}
