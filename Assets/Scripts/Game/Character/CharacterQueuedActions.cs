namespace HardlightProject
{
    // Original Game.Runtime 02000341: complete own eight methods/five fields.
    // Private candidate; genuine Character dependency graph remains unresolved.
    public class CharacterQueuedActions
    {
        private readonly Character m_character; //04000c51
        private bool m_jump;                   //04000c52
        private float m_jumpTime;              //04000c53
        private bool m_boost;                  //04000c54
        private float m_boostTime;             //04000c55

        public CharacterQueuedActions(Character character) //06001463
        {
            m_character = character;
        }

        public bool Jump
        {
            get => m_jump; //06001464
            set //06001465
            {
                // Flag publishes before either real Character call, including faults.
                m_jump = value;
                if (value)
                    m_jumpTime = m_character.GetBrainActionTimestamp(GameAction.CharacterJump);
                else
                    m_character.EndJumpImpulses();
            }
        }

        //06001466: original subtraction is timestamp minus current fixed time.
        // The timestamp read precedes the real Actor clock call.
        public float JumpTimeElapsed => m_jumpTime - m_character.GetTotalFixedTime();

        public bool Boost
        {
            get => m_boost; //06001467
            set //06001468
            {
                m_boost = value;
                if (value)
                    m_boostTime = m_character.GetBrainActionTimestamp(GameAction.CharacterBoost);
            }
        }

        //06001469: the opposite subtraction; the timestamp read follows the clock.
        public float BoostTimeElapsed => m_character.GetTotalFixedTime() - m_boostTime;

        public void ResetBuffers() //0600146a
        {
            // Direct field writes retain timestamps and avoid EndJumpImpulses.
            m_jump = false;
            m_boost = false;
        }
    }
}
