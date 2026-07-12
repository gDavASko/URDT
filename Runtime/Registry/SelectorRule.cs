namespace KBP.URDT.Registry
{
    /// <summary>
    /// A Selector Rule hands a short, stable <c>TestId</c> to objects matching its
    /// predicate. The <see cref="TestIdTemplate"/> is the base id; TestId collisions
    /// are resolved deterministically by the registry with a <c>_{index}</c> suffix
    /// in registration order (04 §4.1).
    /// </summary>
    public sealed class SelectorRule
    {
        private readonly ISelectorPredicate _predicate;
        private readonly string _testIdTemplate;

        public SelectorRule(ISelectorPredicate predicate, string testIdTemplate)
        {
            _predicate = predicate;
            _testIdTemplate = testIdTemplate;
        }

        public ISelectorPredicate Predicate
        {
            get { return _predicate; }
        }

        public string TestIdTemplate
        {
            get { return _testIdTemplate; }
        }
    }
}
