namespace KBP.URDT.Driver
{
    /// <summary>
    /// Reference to a scene for lifecycle operations (reset/reload).
    /// An empty name means "the currently active scene".
    /// </summary>
    public readonly struct SceneRef
    {
        private readonly string _name;

        public SceneRef(string name)
        {
            _name = name;
        }

        public string Name
        {
            get { return _name; }
        }

        public bool IsCurrentScene
        {
            get { return string.IsNullOrEmpty(_name); }
        }
    }
}
