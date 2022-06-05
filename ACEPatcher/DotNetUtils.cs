using System;
using System.Linq;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.PE;

namespace ACEPatcher
{

	public static class DotNetUtils
	{

		public static IDictionary<T, int> CreateObjectToIndexDictionary<T>(IList<T> objs)
		{
			var dict = new Dictionary<T, int>();
			for (int i = 0; i < objs.Count; i++)
				dict[objs[i]] = i;
			return dict;
		}
		public static void CopyBody(MethodDef method, out IList<Instruction> instructions, out IList<ExceptionHandler> exceptionHandlers)
		{
			if (method == null || !method.HasBody)
			{
				instructions = new List<Instruction>();
				exceptionHandlers = new List<ExceptionHandler>();
				return;
			}

			var oldInstrs = method.Body.Instructions;
			var oldExHandlers = method.Body.ExceptionHandlers;
			instructions = new List<Instruction>(oldInstrs.Count);
			exceptionHandlers = new List<ExceptionHandler>(oldExHandlers.Count);
			var oldToIndex = CreateObjectToIndexDictionary(oldInstrs);

			foreach (var oldInstr in oldInstrs)
				instructions.Add(oldInstr.Clone());

			foreach (var newInstr in instructions)
			{
				var operand = newInstr.Operand;
				if (operand is Instruction)
					newInstr.Operand = instructions[oldToIndex[(Instruction)operand]];
				else if (operand is IList<Instruction> oldArray)
				{
					var newArray = new Instruction[oldArray.Count];
					for (int i = 0; i < oldArray.Count; i++)
						newArray[i] = instructions[oldToIndex[oldArray[i]]];
					newInstr.Operand = newArray;
				}
			}

			foreach (var oldEx in oldExHandlers)
			{
				var newEx = new ExceptionHandler(oldEx.HandlerType)
				{
					TryStart = GetInstruction(instructions, oldToIndex, oldEx.TryStart),
					TryEnd = GetInstruction(instructions, oldToIndex, oldEx.TryEnd),
					FilterStart = GetInstruction(instructions, oldToIndex, oldEx.FilterStart),
					HandlerStart = GetInstruction(instructions, oldToIndex, oldEx.HandlerStart),
					HandlerEnd = GetInstruction(instructions, oldToIndex, oldEx.HandlerEnd),
					CatchType = oldEx.CatchType,
				};
				exceptionHandlers.Add(newEx);
			}
		}

		static Instruction GetInstruction(IList<Instruction> instructions, IDictionary<Instruction, int> instructionToIndex, Instruction instruction)
		{
			if (instruction == null)
				return null;
			return instructions[instructionToIndex[instruction]];
		}

		public static void RestoreBody(MethodDef method, IEnumerable<Instruction> instructions, IEnumerable<ExceptionHandler> exceptionHandlers)
		{
			if (method == null || method.Body == null)
				return;

			var bodyInstrs = method.Body.Instructions;
			bodyInstrs.Clear();
			foreach (var instr in instructions)
				bodyInstrs.Add(instr);

			var bodyExceptionHandlers = method.Body.ExceptionHandlers;
			bodyExceptionHandlers.Clear();
			foreach (var eh in exceptionHandlers)
				bodyExceptionHandlers.Add(eh);
		}

		public static void CopyBodyFromTo(MethodDef fromMethod, MethodDef toMethod)
		{
			if (fromMethod == toMethod)
				return;

			CopyBody(fromMethod, out var instructions, out var exceptionHandlers);
			RestoreBody(toMethod, instructions, exceptionHandlers);
			CopyLocalsFromTo(fromMethod, toMethod);
			UpdateInstructionOperands(fromMethod, toMethod);
		}

		static void CopyLocalsFromTo(MethodDef fromMethod, MethodDef toMethod)
		{
			var fromBody = fromMethod.Body;
			var toBody = toMethod.Body;

			toBody.Variables.Clear();
			foreach (var local in fromBody.Variables)
				toBody.Variables.Add(new Local(local.Type));
		}

		static void UpdateInstructionOperands(MethodDef fromMethod, MethodDef toMethod)
		{
			var fromBody = fromMethod.Body;
			var toBody = toMethod.Body;

			toBody.InitLocals = fromBody.InitLocals;
			toBody.MaxStack = fromBody.MaxStack;

			var newOperands = new Dictionary<object, object>();
			var fromParams = fromMethod.Parameters;
			var toParams = toMethod.Parameters;
			for (int i = 0; i < fromParams.Count; i++)
				newOperands[fromParams[i]] = toParams[i];
			for (int i = 0; i < fromBody.Variables.Count; i++)
				newOperands[fromBody.Variables[i]] = toBody.Variables[i];

			foreach (var instr in toBody.Instructions)
			{
				if (instr.Operand == null)
					continue;
				if (newOperands.TryGetValue(instr.Operand, out object newOperand))
					instr.Operand = newOperand;
			}
		}

		public static int ReplaceInstruction(IList<Instruction> instructions, Instruction instruction,params Instruction[] replacement) 
		{
			int index = instructions.IndexOf(instruction);
			if (replacement.Length == 0)
				return 0;
			else if(replacement.Length == 1) 
			{
				instructions[index].OpCode = replacement[0].OpCode;
				instructions[index].Operand = replacement[0].Operand;
				return 1;
			}
            else 
			{
				instructions[index].OpCode = replacement[0].OpCode;
				instructions[index].Operand = replacement[0].Operand;
				((List<Instruction>)instructions).InsertRange(index+1, replacement.Skip(1).ToArray());
				return replacement.Length;
			}
		}

		public static MethodDef findMethodByName(ModuleDefMD[] dependancies,string moduleName,string methodName) 
		{
			ModuleDefMD module = dependancies.Where(x => x.Assembly.FullName == moduleName).First();
			foreach(TypeDef type in module.Types) 
			{
				foreach(MethodDef method in type.Methods) 
				{
					if (method.FullName == methodName)
						return method;
				}
			}
			throw new KeyNotFoundException();
		}

		public static byte[] ReadAssembly(ModuleDefMD module)
		{
			IPEImage image = module.Metadata.PEImage;
			var Reader = image.CreateReader();
			return Reader.ReadBytes((int)Reader.Length);
		}

	}
}