using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace PassengerPlus
{
    internal class TranspileHelper
    {
        protected readonly IEnumerator<CodeInstruction> _baseEnum;
        protected bool _foundMatch;
        public bool FoundMatch => _foundMatch;

        protected bool _continuing = false;

        public TranspileHelper(IEnumerable<CodeInstruction> instructions)
        {
            _baseEnum = instructions.GetEnumerator();
        }

        private EnumeratorWrapper Wrap(IEnumerator<CodeInstruction> target)
        {
            _continuing = true;
            return new EnumeratorWrapper(target);
        }

        public EnumeratorWrapper Until(OpCode opCode, object operand = null)
        {
            return Wrap(new UntilEnumerator(this, _continuing, opCode, operand));
        }

        public EnumeratorWrapper Remaining()
        {
            return Wrap(new BaseEnumerator(this, _continuing));
        }

        #region Enumerators

        internal class EnumeratorWrapper : IEnumerable<CodeInstruction>
        {
            private readonly IEnumerator<CodeInstruction> _target;

            public EnumeratorWrapper(IEnumerator<CodeInstruction> target) => _target = target;

            public IEnumerator<CodeInstruction> GetEnumerator() => _target;

            IEnumerator IEnumerable.GetEnumerator() => _target;

            public void Skip()
            {
                while (_target.MoveNext()) ;
            }
        }

        internal class BaseEnumerator : IEnumerator<CodeInstruction>
        {
            protected TranspileHelper _source;
            protected bool _continuing;

            public BaseEnumerator(TranspileHelper source, bool continuing)
            {
                _source = source;
                _continuing = continuing;
            }

            public CodeInstruction Current => _source._baseEnum.Current;
            object IEnumerator.Current => _source._baseEnum.Current;

            public void Dispose()
            {
                _source = null;
            }

            public virtual bool MoveNext()
            {
                if (_continuing)
                {
                    _continuing = false;
                    return true;
                }

                return _source._baseEnum.MoveNext();
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }

        internal class UntilEnumerator : BaseEnumerator
        {
            private readonly OpCode _opCode;
            private readonly object _operand;

            public UntilEnumerator(TranspileHelper source, bool continuing, OpCode opCode, object operand) : base(source, continuing)
            {
                _opCode = opCode;
                _operand = operand;
            }

            public override bool MoveNext()
            {
                _source._foundMatch = false;
                if (!base.MoveNext()) return false;

                if ((Current.opcode == _opCode) && (_operand is null || Current.OperandIs(_operand)))
                {
                    _source._foundMatch = true;
                    return false;
                }
                return true;
            }
        }
        #endregion
    }
}