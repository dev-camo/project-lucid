using System;
using Hardlight;

namespace ProjectLucid.Verification
{
    // Original Core02000142/060008ce. The generic constraint admits structs;
    // tests preserve reference casts, unboxing copies, and distinct null faults.
    public static class OriginalGraphUserVerification
    {
        private sealed class ReferenceUser : IGraphUser
        {
            public IGraphStorage Storage => throw new InvalidOperationException("Cast must not read storage");
            public void DestroyUser() { throw new InvalidOperationException("Cast must not destroy its user"); }
        }
        private sealed class OtherUser : IGraphUser
        {
            public IGraphStorage Storage => throw new InvalidOperationException("Cast must not read storage");
            public void DestroyUser() { throw new InvalidOperationException("Cast must not destroy its user"); }
        }
        private struct ValueUser : IGraphUser
        {
            public int Value;
            public IGraphStorage Storage => throw new InvalidOperationException("Cast must not read storage");
            public void DestroyUser() { throw new InvalidOperationException("Cast must not destroy its user"); }
        }
        private static void Check(bool value, ref int count, string message)
        { if (!value) throw new InvalidOperationException(message); count++; }

        public static int RunReferenceCasts7()
        {
            int count = 0;
            var owner = new ReferenceUser(); IGraphUser user = owner;
            Check(ReferenceEquals(owner, user.GetAs<ReferenceUser>()), ref count, "Typed cast retains reference identity");
            Check(ReferenceEquals(user, user.GetAs<IGraphUser>()), ref count, "Interface cast retains identity");
            Check(ReferenceEquals(user.GetAs<ReferenceUser>(), user.GetAs<ReferenceUser>()), ref count, "Repeated cast does not allocate");
            Check(((IGraphUser)null).GetAs<ReferenceUser>() == null, ref count, "Null reference cast propagates null");
            Check(((IGraphUser)null).GetAs<IGraphUser>() == null, ref count, "Null interface cast propagates null");
            bool wrong = false; try { new OtherUser().GetAs<ReferenceUser>(); } catch (InvalidCastException) { wrong = true; }
            Check(wrong, ref count, "Wrong reference type throws InvalidCastException");
            Check(ReferenceEquals(owner, user), ref count, "Failed cast leaves original user intact");
            return count;
        }

        public static int RunValueCasts5()
        {
            int count = 0;
            IGraphUser user = new ValueUser { Value = 37 };
            Check(user.GetAs<ValueUser>().Value == 37, ref count, "Typed cast unboxes original value");
            ValueUser copy = user.GetAs<ValueUser>(); copy.Value = 99;
            Check(user.GetAs<ValueUser>().Value == 37, ref count, "Unboxed mutation does not change original box");
            Check(copy.Value == 99, ref count, "Unboxed local has independent value");
            bool nullValue = false; try { ((IGraphUser)null).GetAs<ValueUser>(); } catch (NullReferenceException) { nullValue = true; }
            Check(nullValue, ref count, "Null unboxing faults instead of returning default");
            bool wrongValue = false; try { new ReferenceUser().GetAs<ValueUser>(); } catch (InvalidCastException) { wrongValue = true; }
            Check(wrongValue, ref count, "Wrong boxed type throws InvalidCastException");
            return count;
        }
    }
}
