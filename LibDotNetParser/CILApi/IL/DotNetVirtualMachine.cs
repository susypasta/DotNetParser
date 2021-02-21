﻿using LibDotNetParser.CILApi.IL;
using System;
using System.Collections.Generic;

namespace LibDotNetParser.CILApi
{
    public class DotNetVirtualMachine
    {
        DotNetFile file;
        /// <summary>
        /// For debuging
        /// </summary>
        public bool DumpIL = true;
        int argsCurrentIndex = 0;
        object[] Args = new object[12];

        public DotNetVirtualMachine(DotNetFile file)
        {
            this.file = file;
        }
        /// <summary>
        /// Calls the entrypoint in the file.
        /// </summary>
        public void Start()
        {
            DotNetMethod m = null;
            foreach (var item in file.Types)
            {
                foreach (var m2 in item.Methods)
                {
                    if (m2.IsStatic && m2.Name == "Main")
                    {
                        m = m2;
                        break;
                    }
                }
            }
            if (m == null)
                throw new System.Exception("Invaild .NET EXE: Entry Point not found!");
            ProcessMethod(m, "Arg 1", "Arg 2");
        }
        /// <summary>
        /// Makes things much easier for the DoStartMethod() function
        /// </summary>
        /// <param name="m"></param>
        /// <param name="args"></param>
        // All of the Opcodes that have additional parameters have to be processed here.
        private void ProcessMethod(DotNetMethod m, params string[] args)
        {
            byte[] code = m.GetBody();
            List<ILInstruction> inr = new List<ILInstruction>();

            for (int i = 0; i < code.Length; i++)
            {
                byte opCode = code[i];

                if (opCode == (byte)OpCodes.Ldstr)
                {
                    //Decode the number
                    byte first = code[i + 1]; //1st index
                    byte sec = code[i + 2]; //2nd
                    byte third = code[i + 3];
                    byte forth = code[i + 4];
                    byte[] num = new byte[] { first, sec, third, 0 };
                    var numb = BitConverter.ToInt32(num, 0);

                    //Get the string
                    string s;

                    //Microsoft does really good documentation on front-end things. For example: Windows Apis, and the dot net framework.
                    //But, They don't do backend documentation, for example: Decoding this string token. 
                    //I have to go through 100,000+ lines of code in the .NET Clr to figure out how these string tokens work and still didnt figure it out.

                    if (forth != 112)
                    {
                        //Will this ever be in the String Stream?
                        s = file.Backend.ClrStringsStream.GetByOffset((uint)numb);
                    }
                    else
                    {
                        //US stream

                        //This is only a temp. hack
                        s = file.Backend.ClrUsStream.GetByOffset((uint)numb);
                    }
                    int rid = numb & 0x00ffffff; //Not sure weather this is needed, but I found it in the CLR
                    i += 4; //skip past the string

                    inr.Add(new ILInstruction()
                    {
                        OpCode = OpCodes.Ldstr,
                        Operand = s
                    });
                }
                else if (opCode == OpCodes.Call)
                {
                    try
                    {
                        byte fi = code[i + 1];
                        byte s = code[i + 2];
                        byte t = code[i + 3];
                        byte f = code[i + 4];
                        byte[] num = new byte[] { fi, s, t, f };
                        short numb = BitConverter.ToInt16(num, 0); //Method Token


                        //Get the method that we are calling
                        var c = file.Backend.tabels.MemberRefTabelRow[numb - 1]; //is the -1 needed?

                        i += 4; //skip past the string
                        #region Decode
                        //Decode the class bytes
                        uint tabel;
                        uint row;
                        DecodeMemberRefParent(c.Class, out tabel, out row);


                        var funcName = file.Backend.ClrStringsStream.GetByOffset(c.Name);
                        string classs;
                        string Namespace;

                        //TYPE def
                        if (tabel == 02)
                        {
                            var tt = file.Backend.tabels.TypeDefTabel[(int)row - 1];

                            classs = file.Backend.ClrStringsStream.GetByOffset(tt.Name);
                            Namespace = file.Backend.ClrStringsStream.GetByOffset(tt.Namespace);
                        }
                        //Type REF
                        else if (tabel == 01)
                        {
                            var tt = file.Backend.tabels.TypeRefTabel[(int)row - 1];

                            classs = file.Backend.ClrStringsStream.GetByOffset(tt.TypeName);
                            Namespace = file.Backend.ClrStringsStream.GetByOffset(tt.TypeNamespace);
                        }
                        //Module Ref
                        else if (tabel == 26)
                        {
                            //var tt = file.Backend.MetaDataStreamTablesHeader.Tables.ModuleRef[(int)row - 1];

                            //classs = file.Backend.ClrStringsStream.GetByOffset(tt.Name);
                            //Namespace = file.Backend.ClrStringsStream.GetByOffset(tt.Namespace);
                            Console.WriteLine("Module Ref not supported!");
                            classs = "<Module Ref>";
                            Namespace = "<Module Ref>";
                        }
                        //Unknown
                        else
                        {
                            classs = "<unknown>";
                            Namespace = "<unknown>";
                        }
                        #endregion
                        var inst = new ILInstruction()
                        {
                            OpCode = OpCodes.Call,
                            DecompilerExtraData = numb
                        };
                        inst.Operand = new CallMethodDataHolder() { ClassName = classs, NameSpace = Namespace, FunctionName = funcName };


                        inr.Add(inst);
                    }
                    catch { }
                }
                else
                {
                    inr.Add(new ILInstruction() { OpCode = opCode });
                }
            }
            //After decoding, start the method
            DoStartMethod(inr, args);
        }
        private void DoStartMethod(List<ILInstruction> ops, params string[] args)
        {
            foreach (var item in ops)
            {
                if (item.OpCode == OpCodes.Nop)
                {
                    //Don't do anything
                    if (DumpIL)
                        Console.WriteLine("[IL] NOP");
                }

                else if (item.OpCode == OpCodes.Ldstr)
                {
                    Args[argsCurrentIndex] = (string)item.Operand;
                    argsCurrentIndex++;
                    if (DumpIL)
                        Console.WriteLine("[IL] Ldstr \"" + (string)item.Operand + "\"");
                }
                else if (item.OpCode == OpCodes.Call)
                {
                    var x = (CallMethodDataHolder)item.Operand;

                    //Call function
                    if (DumpIL)
                        Console.WriteLine("[IL] Call " + x.NameSpace + "." + x.ClassName + "." + x.FunctionName);

                    //TODO: Load these functions from some other DLL. For now, hard code these in.
                    if (x.NameSpace == "System" && x.ClassName == "Console" && x.FunctionName == "WriteLine")
                    {
                        Console.WriteLine("[Program Output] " + Args[0]);
                    }
                    else if (x.NameSpace == "System" && x.ClassName == "Console" && x.FunctionName == "ReadLine")
                    {
                        Args[0] = Console.ReadLine();
                    }
                    else
                    {
                        Console.WriteLine("Calling external/framework/internal functions not supported! Function Name: " + x.NameSpace + "." + x.ClassName + "." + x.FunctionName + "()");
                    }
                    argsCurrentIndex = 0;
                }
                else if (item.OpCode == OpCodes.Ldarg_0)
                {
                    Args[0] = args[0];
                }
                else if (item.OpCode == OpCodes.Ret)
                {
                    if (DumpIL)
                        Console.WriteLine("[IL] RET");
                    return;
                }
                else
                {
                    if (DumpIL)
                        Console.WriteLine("[IL] Unknown OPCODE: " + item.OpCode);
                }
            }
        }

        #region Decoding MemberRefParent
        private const uint MemberRefParrent = 0x7;
        private const uint MemberRefParrent_TYPEDEF = 0x0;
        private const uint MemberRefParrent_TYPEREF = 0x1;
        private const uint MemberRefParrent_MODULEREF = 0x2;
        private const uint MemberRefParrent_METHODDEF = 0x3;
        private const uint MemberRefParrent_TYPESPEC = 0x4;
        private static void DecodeMemberRefParent(uint index, out uint tableIndex, out uint row)
        {
            tableIndex = 0;
            switch (index & MemberRefParrent)
            {
                case MemberRefParrent_TYPEDEF:
                    tableIndex = 02;
                    break;

                case MemberRefParrent_TYPEREF:
                    tableIndex = 01;
                    break;

                case MemberRefParrent_MODULEREF:
                    tableIndex = 26;
                    break;

                case MemberRefParrent_METHODDEF:
                    tableIndex = 06;
                    break;

                case MemberRefParrent_TYPESPEC:
                    tableIndex = 27;
                    break;
            }
            row = index >> 3;
        }
        #endregion
    }
}