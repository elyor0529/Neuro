﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Drawing;
using System.IO;
using Neuro.Tensors;

namespace Neuro
{
    public static class Tools
    {
        public static readonly double _EPSILON = 10e-7;

        public static Random Rng = new Random();

        public static int BinaryClassificationEquality(Tensor target, Tensor output)
        {
            int hits = 0;
            for (int n = 0; n < output.Batches; ++n)
                hits += target[0, 0, 0, n].Equals(Math.Round(output[0, 0, 0, n])) ? 1 : 0;
            return hits;
        }

        public static int CategoricalClassificationEquality(Tensor target, Tensor output)
        {
            int hits = 0;
            for (int n = 0; n < output.Batches; ++n)
                hits += target.ArgMax(n).Equals(output.ArgMax(n)) ? 1 : 0;
            return hits;
        }

        public static double Clip(double v, double min, double max)
        {
            if (v < min)
                return min;
            if (v > max)
                return max;
            return v;
        }

        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n-- > 1)
            {
                int k = Tools.Rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static string GetProgressString(int iteration, int maxIterations, string extraStr = "", int barLength = 30)
        {
            int maxIterLen = maxIterations.ToString().Length;
            double step = maxIterations / (double)barLength;
            int currStep = (int)Math.Min(Math.Ceiling(iteration / step), barLength);
            return $"{iteration.ToString().PadLeft(maxIterLen)}/{maxIterations} [{new string('=', currStep - 1)}" + (iteration == maxIterations ? "=" : ">") + $"{new string('.', barLength - currStep)}]" + extraStr;
        }

        public static int ReadBigInt32(this BinaryReader br)
        {
            var bytes = br.ReadBytes(sizeof(Int32));
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        public static void WriteBigInt32(this BinaryWriter bw, Int32 v)
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)v;
            bytes[1] = (byte)(((uint)v >> 8) & 0xFF);
            bytes[2] = (byte)(((uint)v >> 16) & 0xFF);
            bytes[3] = (byte)(((uint)v >> 24) & 0xFF);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            bw.Write(BitConverter.ToInt32(bytes, 0));
        }

        public static List<Data> ReadMnistData(string imagesFile, string labelsFile, bool generateBmp = false, int maxImages = -1)
        {
            List<Data> dataSet = new List<Data>();

            using (FileStream fsLabels = new FileStream(labelsFile, FileMode.Open))
            using (FileStream fsImages = new FileStream(imagesFile, FileMode.Open))
            using (BinaryReader brLabels = new BinaryReader(fsLabels))
            using (BinaryReader brImages = new BinaryReader(fsImages))
            {
                int magic1 = brImages.ReadBigInt32(); // discard
                int numImages = brImages.ReadBigInt32();
                int imgWidth = brImages.ReadBigInt32();
                int imgHeight = brImages.ReadBigInt32();

                int magic2 = brLabels.ReadBigInt32(); // 2039 + number of outputs
                int numLabels = brLabels.ReadBigInt32();

                maxImages = maxImages < 0 ? numImages : Math.Min(maxImages, numImages);

                int outputsNum = magic2 - 2039;

                Bitmap bmp = null;
                int bmpRows = (int)Math.Ceiling(Math.Sqrt((double)maxImages));
                int bmpCols = (int)Math.Ceiling(Math.Sqrt((double)maxImages));

                if (generateBmp)
                    bmp = new Bitmap(bmpCols * imgHeight, bmpRows * imgWidth);

                for (int i = 0; i < maxImages; ++i)
                {
                    Tensor input = new Tensor(new Shape(imgWidth, imgHeight));
                    Tensor output = new Tensor(new Shape(1, outputsNum));

                    for (int y = 0; y < imgWidth; ++y)
                    for (int x = 0; x < imgHeight; ++x)
                    {
                        byte color = brImages.ReadByte();
                        input[x, y] = (double)color / 255;
                        bmp?.SetPixel((i % bmpCols) * imgWidth + x, (i / bmpCols) * imgHeight + y, Color.FromArgb(color, color, color));
                    }

                    byte lbl = brLabels.ReadByte();
                    output[0, lbl] = 1;

                    dataSet.Add(new Data() { Input = input, Output = output });
                }

                using (bmp)
                    bmp?.Save($"{imagesFile.Split('.')[0]}.bmp");
            }

            return dataSet;
        }

        public static void WriteMnistData(List<Data> data, string imagesFile, string labelsFile)
        {
            if (data.Count == 0)
                return;

            using (FileStream fsLabels = new FileStream(labelsFile, FileMode.Create))
            using (FileStream fsImages = new FileStream(imagesFile, FileMode.Create))
            using (BinaryWriter bwLabels = new BinaryWriter(fsLabels))
            using (BinaryWriter bwImages = new BinaryWriter(fsImages))
            {
                int imgHeight = data[0].Input.Height;
                int imgWidth = data[0].Input.Width;
                int outputsNum = data[0].Output.Length;

                bwImages.WriteBigInt32(1337); // discard
                bwImages.WriteBigInt32(data.Count);
                bwImages.WriteBigInt32(imgHeight);
                bwImages.WriteBigInt32(imgWidth);

                bwLabels.WriteBigInt32(2039 + outputsNum);
                bwLabels.WriteBigInt32(data.Count);

                for (int i = 0; i < data.Count; ++i)
                {
                    for (int h = 0; h < imgHeight; ++h)
                    for (int x = 0; x < imgWidth; ++x)
                        bwImages.Write((byte)(data[i].Input[h, x] * 255));

                    for (int j = 0; j < outputsNum; ++j)
                    {
                        if (data[i].Output[j] == 1)
                        {
                            bwLabels.Write((byte)j);
                        }
                    }
                }
            }
        }

        public static List<Data> LoadCSVData(string filename, int outputs, bool outputsOneHotEncoded = false)
        {
            List<Data> dataSet = new List<Data>();

            using (var f = new StreamReader(filename))
            {
                string line;
                while ((line = f.ReadLine()) != null)
                {
                    string[] tmp = line.Split(',');

                    Tensor input = new Tensor(new Shape(1, tmp.Length - (outputsOneHotEncoded ? 1 : outputs)));
                    Tensor output = new Tensor(new Shape(1, outputs));

                    for (int i = 0; i < input.Length; ++i)
                        input[0, i] = double.Parse(tmp[i]);

                    for (int i = 0; i < (outputsOneHotEncoded ? 1 : outputs); ++i)
                    {
                        double v = double.Parse(tmp[input.Length + i]);
                        if (outputsOneHotEncoded)
                            output[0, (int)v] = 1;
                        else
                            output[0, i] = v;
                    }

                    dataSet.Add(new Data() { Input = input, Output = output });
                }
            }

            return dataSet;
        }

        public static List<Data> MergeData(List<Data> dataList, int batchSize = -1)
        {
            if (batchSize < 0)
                batchSize = dataList.Count;

            List<Data> mergedData = new List<Data>();

            int batchesNum = dataList.Count / batchSize;

            for (int b = 0; b < batchesNum; ++b)
            {
                mergedData.Add(new Data() { Input = Tensor.Merge(dataList.GetRange(b * batchSize, batchSize).Select(x => x.Input).ToList()),
                                            Output = Tensor.Merge(dataList.GetRange(b * batchSize, batchSize).Select(x => x.Output).ToList())});
            }

            // add support for reminder of training data
            Debug.Assert(dataList.Count % batchSize == 0);

            return mergedData;
        }
    }
}