namespace AccessTheObelisk
{
    internal static class NavigationBounds
    {
        internal static bool TryMove(ref int index, int delta, int count)
        {
            if (count <= 0)
            {
                return false;
            }

            if (count == 1)
            {
                index = 0;
                return true;
            }

            int next = ClampIndex(index + delta, count);
            if (next == index)
            {
                return false;
            }

            index = next;
            return true;
        }

        internal static bool TryJump(ref int index, bool end, int count)
        {
            if (count <= 0)
            {
                return false;
            }

            if (count == 1)
            {
                index = 0;
                return true;
            }

            int next = end ? count - 1 : 0;
            if (next == index)
            {
                return false;
            }

            index = next;
            return true;
        }

        internal static int ClampIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return 0;
            }

            return index >= count ? count - 1 : index;
        }
    }
}
