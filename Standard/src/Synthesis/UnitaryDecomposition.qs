// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Quantum.Synthesis {
    open Microsoft.Quantum.Arithmetic;
    open Microsoft.Quantum.Arrays;
    open Microsoft.Quantum.Canon;
    open Microsoft.Quantum.Diagnostics;
    open Microsoft.Quantum.Intrinsic;
    open Microsoft.Quantum.Math;
    open Microsoft.Quantum.Logical;

    // # Summary
    /// Applies single-qubit gate defined by 2x2 unitary matrix.
    ///
    /// # Input
    /// ## u
    /// 2x2 unitary matrix describing the operation. 
    /// Assumed to be unitary. If it's not unitary, behaviour is undefined.
    /// ## qubit
    /// Qubit to which apply the operation.
    internal operation ApplySingleQubitUnitary(u: Complex[][], qubit : Qubit) : Unit is Adj + Ctl {  
        // ZYZ decomposition.
        let theta = ArcCos(AbsComplex(u[0][0]));
        let lmbda = ArgComplex(u[0][0]);
        let mu = ArgComplex(u[0][1]);
        if (AbsD(mu-lmbda) > 1e-9) { Rz(mu - lmbda, qubit); }
        if (AbsD(theta) > 1e-9) { Ry(-2.0 * theta, qubit); }
        if (AbsD(lmbda + mu) > 1e-9) { Rz(- lmbda - mu, qubit); }

        // If this is not special unitary, apply R1 to correct global phase.
        let det = MinusC(TimesC(u[0][0], u[1][1]), TimesC(u[0][1], u[1][0]));
        let phi = ArgComplex(det);
        if(AbsD(phi) > 1e-9) {R1(phi, qubit);}
    }

    internal function TwoLevelDecomposition(unitary: Complex[][]) : (Complex[][], Int, Int)[] {
        body intrinsic;
    }

    // For every 2-level unitary calculates "flip mask", which denotes qubit which should 
    // be inverted before and after applying corresponding 1-qubit gate.
    // For convenience prepends result with 0.
    internal function GetFlipMasks(decomposition: (Complex[][], Int, Int)[], allQubitsMask: Int) : Int[] {
        let n = Length(decomposition);
        mutable flipMasks = new Int[n+1];
        set flipMasks w/= 0 <- 0;
        for (i in 0..n-1) {
            let (matrix, i1, i2) = decomposition[i];
            set flipMasks w/= (i+1) <- (allQubitsMask - i2); 
        }
        return flipMasks;
    }

    // Applies X gates to qubits speicifed by flipMask.
    internal operation ApplyFlips(flipMask: Int, qubits : LittleEndian) : Unit is Adj + Ctl  {
        for (qubitId in 0..Length(qubits!)-1) {
            if ((flipMask &&& (1 <<< qubitId)) != 0) { X(qubits![qubitId]); }    
        }
    }

    // # Summary
    /// Applies gate defined by 2^n x 2^n unitary matrix.
    ///
    /// # Input
    /// ## unitary
    /// 2^n x 2^n unitary matrix describing the operation. 
    /// If matrix is not unitary or not of suitable size, throws an exception.
    /// ## qubit
    /// Qubits to which apply the operation - register of length n.
    operation ApplyUnitary(unitary: Complex[][], qubits : LittleEndian) : Unit is Adj + Ctl {
        if (Length(unitary) != 1 <<< Length(qubits!)) {
            fail "Matrix size is not consistent with register length.";
        }
        let allQubitsMask = (1 <<< Length(qubits!)) - 1;
        let decomposition = TwoLevelDecomposition(unitary);
        let flipMasks = GetFlipMasks(decomposition, allQubitsMask);
        
        for (i in 0..Length(decomposition)-1) {
            // i1, i2 - indices of non-trivial 2x2 submatrix of two-level unitary matrix being applied.
            // They differ in exactly one bit; i1 < i2.
            // matrix - 2x2 non-trivial unitary submatrix of said two-level unitary.
            let (matrix, i1, i2) = decomposition[i];
            Message("${matrix}, {i1}, {i2}");

            ApplyFlips(flipMasks[i+1] ^^^ flipMasks[i], qubits);

            let targetMask = i1 ^^^ i2;
            let controlMask = allQubitsMask - targetMask;
            let (controls, targets) = MaskToQubitsPair(qubits!, MCMTMask(controlMask, targetMask));
            Controlled ApplySingleQubitUnitary(controls, (matrix, targets[0])); 
        }   
        ApplyFlips(Tail(flipMasks), qubits);
    }    
}
