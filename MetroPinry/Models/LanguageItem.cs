namespace MetroPinry.Models
{
    public class LanguageItem
    {
        public string Code { get; set; }        // "en-US", "ru-RU", "de-DE"
        public string NativeName { get; set; }  // "English", "Русский", "Deutsch"
        public bool IsSelected { get; set; }
    }
}