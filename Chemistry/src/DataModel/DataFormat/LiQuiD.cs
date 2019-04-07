﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Quantum.Simulation.Core;

using System.Text.RegularExpressions;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Microsoft.Quantum.Chemistry
{
    /// <summary>
    /// Methods for loading Hamiltonian data from standard formats
    /// into a <see cref="FermionHamiltonian"/>.
    /// </summary>
    public class LiQuiD
    {
        /// <summary>
        ///      Loads a Hamiltonian from integral data represented
        ///      in LIQ𝑈𝑖|⟩ format.
        ///      Please see the <a href="https://stationq.github.io/Liquid/docs/LIQUiD.pdf">
        ///      LIQ𝑈𝑖|⟩ documentation</a> for further details about the
        ///      format parsed by this method.
        /// </summary>
        /// <param name="filename">The name of the file to be loaded.</param>
        /// <returns>
        ///      An instance of <see cref="FermionHamiltonian"/> representing the
        ///      data contained in <paramref name="filename"/>.
        /// </returns>
        public static IEnumerable<OrbitalIntegralHamiltonian> LoadMultipleFromLiquid(string filename)
        {
            var name = filename;
            var allText = System.IO.File.ReadAllText(filename, System.Text.Encoding.ASCII);
            string[] delimiters = { "tst" };
            var lines = allText.Split(delimiters, System.StringSplitOptions.RemoveEmptyEntries);
            var hamiltonians = lines.Select(o => LoadFromLiquid(o));
            return hamiltonians;
        }

        /// <summary>
        ///      Loads a Hamiltonian from integral data represented
        ///      in LIQ𝑈𝑖|⟩ format.
        ///      Please see the <a href="https://stationq.github.io/Liquid/docs/LIQUiD.pdf">
        ///      LIQ𝑈𝑖|⟩ documentation</a> for further details about the
        ///      format parsed by this method.
        /// </summary>
        /// <param name="lines">Sequence of text describing terms of Hamiltonian.</param>
        /// <returns>
        ///      An instance of <see cref="FermionHamiltonian"/> representing the
        ///      data contained in <paramref name="lines"/>.
        /// </returns>
        public static OrbitalIntegralHamiltonian LoadFromLiquid(string line)
        {
            var regexMiscellaneous = new Regex(@"((info=(?<info>[^\s]*)))");
            var regexnuc = new Regex(@"nuc=(?<nuc>-?\s*\d*.\d*)");
            var regexPQ = new Regex(@"(^|\s+)(?<p>\d+),(?<q>\d+)\D*=\s*(?<coeff>-?\s*\d*.\d*)e?(?<exponent>-?\d*)");
            var regexPQRS = new Regex(@"(^|\s+)(?<p>\d+)\D+(?<q>\d+)\D+(?<r>\d+)\D+(?<s>\d+)\D*=\s*(?<coeff>-?\s*\d*.\d*)e?(?<exponent>-?\d*)");

            Double coulombRepulsion = 0.0;
            var fileHIJTerms = new Dictionary<int[], Double>(new Extensions.IntArrayIEqualityComparer());
            var fileHIJKLTerms = new Dictionary<int[], Double>(new Extensions.IntArrayIEqualityComparer());
            var hamiltonian = new OrbitalIntegralHamiltonian();

            var nOrbitals = 0L;

            Match stringMisc = regexMiscellaneous.Match(line);
            if (stringMisc.Success)
            {
                //hamiltonian.MiscellaneousInformation = stringMisc.Groups["info"].ToString();
            }

            Match stringnuc = regexnuc.Match(line);
            if (stringnuc.Success)
            {
                hamiltonian.AddTerm(TermType.OrbitalIntegral.Identity, new OrbitalIntegral(), double.Parse(stringnuc.Groups["nuc"].ToString()));
            }
            foreach (Match stringPQ in regexPQ.Matches(line))
            {
                if (stringPQ.Success)
                {
                    var p = int.Parse(stringPQ.Groups["p"].ToString());
                    var q = int.Parse(stringPQ.Groups["q"].ToString());
                    var coeff = double.Parse(stringPQ.Groups["coeff"].ToString());
                    var exponentString = stringPQ.Groups["exponent"].ToString();
                    var exponent = 0.0;
                    if (exponentString != "")
                    {
                        exponent = Double.Parse(stringPQ.Groups["exponent"].ToString());
                    }
                    nOrbitals = new long[] { nOrbitals, p + 1, q + 1 }.Max();

                    var orbitalIntegral = new OrbitalIntegral(new int[] { p, q }, coeff * (10.0).Pow(exponent));
                    var orbitalIntegralCanonical = orbitalIntegral.ToCanonicalForm();

                    //Logger.Message.WriteLine($"1e orbital { orbitalIntegral.Print()}, { orbitalIntegralCanonical.Print()}");

                    if (fileHIJTerms.ContainsKey(orbitalIntegralCanonical.OrbitalIndices))
                    {
                        // Check consistency
                        if (fileHIJTerms[orbitalIntegralCanonical.OrbitalIndices] != orbitalIntegral.Coefficient)
                        {
                            // Consistency check failed.
                            throw new System.NotSupportedException(
                                $"fileHPQTerm Consistency check fail. " +
                                $"Orbital integral {orbitalIntegral} coefficient {orbitalIntegral.Coefficient}" +
                                $"does not match recorded {orbitalIntegralCanonical} coefficient {fileHIJTerms[orbitalIntegral.OrbitalIndices]}.");
                        }
                        else
                        {
                            // Consistency check passed.
                        }
                        //Logger.Message.WriteLine($"1e orbital collision { orbitalIntegral.Print()}, { orbitalIntegralCanonical.Print()}");
                    }
                    else
                    {
                        fileHIJTerms.Add(orbitalIntegralCanonical.OrbitalIndices, orbitalIntegralCanonical.Coefficient);
                    }
                }
            }
            foreach (Match stringPQRS in regexPQRS.Matches(line))
            {
                if (stringPQRS.Success)
                {
                    var p = int.Parse(stringPQRS.Groups["p"].ToString());
                    var q = int.Parse(stringPQRS.Groups["q"].ToString());
                    var r = int.Parse(stringPQRS.Groups["r"].ToString());
                    var s = int.Parse(stringPQRS.Groups["s"].ToString());
                    var coeff = double.Parse(stringPQRS.Groups["coeff"].ToString());
                    var exponentString = stringPQRS.Groups["exponent"].ToString();
                    var exponent = 0.0;
                    if (exponentString != "")
                    {
                        exponent = double.Parse(stringPQRS.Groups["exponent"].ToString());
                    }
                    nOrbitals = new long[] { nOrbitals, p + 1, q + 1, r + 1, s + 1 }.Max();

                    var orbitalIntegral = new OrbitalIntegral(new int[] { p, q, r, s }, coeff * (10.0).Pow(exponent));
                    var orbitalIntegralCanonical = orbitalIntegral.ToCanonicalForm();

                    //Logger.Message.WriteLine($"2e orbital: { orbitalIntegral.Print()}, { orbitalIntegralCanonical.Print()}");

                    if (fileHIJKLTerms.ContainsKey(orbitalIntegralCanonical.OrbitalIndices))
                    {
                        // Check consistency
                        if (fileHIJKLTerms[orbitalIntegralCanonical.OrbitalIndices] != orbitalIntegral.Coefficient)
                        {
                            // Consistency check failed.
                            throw new System.NotSupportedException(
                                $"fileHPQRSTerm Consistency check fail. " +
                                $"Orbital integral {orbitalIntegral.OrbitalIndices} coefficient {orbitalIntegral.Coefficient}" +
                                $"does not match recorded {orbitalIntegralCanonical.OrbitalIndices} coefficient {fileHIJKLTerms[orbitalIntegral.OrbitalIndices]}.");
                        }
                        else
                        {
                            // Consistency check passed.
                        }
                        //Logger.Message.WriteLine($"2e orbital collision { orbitalIntegral.Print()}, { orbitalIntegralCanonical.Print()}");
                    }
                    else
                    {
                        fileHIJKLTerms.Add(orbitalIntegralCanonical.OrbitalIndices, orbitalIntegralCanonical.Coefficient);
                    }
                }
            }

            //hamiltonian.NOrbitals = nOrbitals;
            foreach (var ijTerm in fileHIJTerms)
            {
                hamiltonian.AddTerm(new OrbitalIntegral(ijTerm.Key, ijTerm.Value));
            }
            foreach (var ijklTerm in fileHIJKLTerms)
            {
                hamiltonian.AddTerm(new OrbitalIntegral(ijklTerm.Key, ijklTerm.Value));
            }
            return hamiltonian;
        }
    }
}