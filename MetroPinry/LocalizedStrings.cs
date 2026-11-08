using System.ComponentModel;
using MetroPinry.Resources;

namespace MetroPinry
{
    public class LocalizedStrings : INotifyPropertyChanged
    {
        private static AppResources _localizedResources = new AppResources();

        public AppResources LocalizedResources
        {
            get { return _localizedResources; }
        }

        public static LocalizedStrings Instance { get; private set; }

        public LocalizedStrings()
        {
            Instance = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static void UpdateLanguage()
        {
            _localizedResources = new AppResources();
            if (Instance != null && Instance.PropertyChanged != null)
            {
                Instance.PropertyChanged(Instance, new PropertyChangedEventArgs("LocalizedResources"));
            }
        }
    }
}