﻿using LibDotNetParser.CILApi.IL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace LibDotNetParser.CILApi
{
    /// <summary>
    /// Represenets an IL Instruction
    /// </summary>
    public class ILInstruction
    {
        /// <summary>
        /// The opcode
        /// </summary>
        public int OpCode { get; set; }
        /// <summary>
        /// The operand
        /// </summary>
        public object Operand { get; set; }
        /// <summary>
        /// OpCode Name
        /// </summary>
        public string OpCodeName { get; set; }

        public OpCodeOperandType OperandType { get; set; }
    }
}