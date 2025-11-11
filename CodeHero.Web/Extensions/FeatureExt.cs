// small helper to avoid null checks for feature-setting
internal static class FeatureExt
{
    public static void Let<T>(this T? obj, Action<T> action) where T : class
    {
        if (obj is not null) action(obj);
    }
}