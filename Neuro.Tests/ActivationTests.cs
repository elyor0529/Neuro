﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neuro.Tensors;

namespace Neuro.Tests
{
    [TestClass]
    public class ActivationTests
    {
        [TestMethod]
        public void Sigmoid_Derivative_1Batch()
        {
            Tools.VerifyFuncDerivative(Activation.Sigmoid);
        }

        [TestMethod]
        public void Sigmoid_Derivative_3Batches()
        {
            Tools.VerifyFuncDerivative(Activation.Sigmoid, 3);
        }

        [TestMethod]
        public void ReLU_Derivative_1Batch()
        {
            Tools.VerifyFuncDerivative(Activation.ReLU);
        }

        [TestMethod]
        public void ReLU_Derivative_3Batches()
        {
            Tools.VerifyFuncDerivative(Activation.ReLU, 3);
        }

        [TestMethod]
        public void Tanh_Derivative_1Batch()
        {
            Tools.VerifyFuncDerivative(Activation.Tanh);
        }

        [TestMethod]
        public void Tanh_Derivative_3Batches()
        {
            Tools.VerifyFuncDerivative(Activation.Tanh, 3);
        }

        [TestMethod]
        public void ELU_Derivative_1Batch()
        {
            Tools.VerifyFuncDerivative(Activation.ELU);
        }

        [TestMethod]
        public void ELU_Derivative_3Batches()
        {
            Tools.VerifyFuncDerivative(Activation.ELU, 3);
        }

        [TestMethod]
        public void Softmax_Derivative_1Batch()
        {
            var input = new Tensor(new Shape(1, 3));
            input.FillWithRange(1);

            var output = new Tensor(input.Shape);
            Activation.Softmax.Compute(input, output);

            //var outputGradient = new Tensor(input.Shape);
            //Loss.CategoricalCrossEntropy(new Tensor(new[] { 1.0, 0.0, 0.0 }, input.Shape), output, true, outputGradient);

            var outputGradient = new Tensor(input.Shape);
            outputGradient.FillWithValue(1.0);

            var result = new Tensor(input.Shape);
            Activation.Softmax.Derivative(output, outputGradient, result);

            for (int i = 0; i < input.Shape.Length; ++i)
                Assert.AreEqual(result.GetFlat(i), 0, 1e-3);
        }

        [TestMethod]
        public void Softmax_Derivative_3Batches()
        {
            var input = new Tensor(new Shape(1, 3, 1, 3));
            input.FillWithRange(1);

            var output = new Tensor(input.Shape);
            Activation.Softmax.Compute(input, output);

            //var outputGradient = new Tensor(input.Shape);
            //Loss.CategoricalCrossEntropy(new Tensor(new[] { 1.0, 0.0, 0.0 }, input.Shape), output, true, outputGradient);

            var outputGradient = new Tensor(input.Shape);
            outputGradient.FillWithValue(1.0);

            var result = new Tensor(input.Shape);
            Activation.Softmax.Derivative(output, outputGradient, result);

            for (int i = 0; i < input.Shape.Length; ++i)
                Assert.AreEqual(result.GetFlat(i), 0, 1e-3);
        }

        [TestMethod]
        public void Softmax_1Batch()
        {
            var input = new Tensor(new Shape(3, 3, 3, 1));
            input.FillWithRand();

            var result = new Tensor(input.Shape);
            Activation.Softmax.Compute(input, result);

            Assert.AreEqual(result.Sum(0), 1, 1e-4);
        }

        [TestMethod]
        public void Softmax_3Batches()
        {
            var input = new Tensor(new Shape(3, 3, 3, 3));
            input.FillWithRand();

            var result = new Tensor(input.Shape);
            Activation.Softmax.Compute(input, result);

            for (int b = 0; b < 3; ++b)
                Assert.AreEqual(result.Sum(b), 1, 1e-4);
        }
    }
}