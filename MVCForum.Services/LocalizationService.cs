﻿using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Globalization;
using System.Linq;
using System.Text;
using MVCForum.Domain;
using MVCForum.Domain.Constants;
using MVCForum.Domain.DomainModel;
using MVCForum.Domain.Interfaces;
using MVCForum.Domain.Interfaces.Repositories;
using MVCForum.Domain.Interfaces.Services;
using MVCForum.Utilities;

namespace MVCForum.Services
{

    public class LocalizationService : ILocalizationService
    {
        private readonly ILocalizationRepository _localizationRepository;
        private readonly ISettingsRepository _settingsRepository;
        private readonly ILoggingService _loggingService;

        private Language _currentLanguage;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="localizationRepository"> </param>
        /// <param name="settingsRepository"> </param>
        public LocalizationService(ILocalizationRepository localizationRepository, ISettingsRepository settingsRepository, ILoggingService loggingService)
        {
            _localizationRepository = localizationRepository;
            _settingsRepository = settingsRepository;
            _loggingService = loggingService;
        }

        #region Private methods

        /// <summary>
        /// Update a resource string
        /// </summary>
        /// <param name="languageId"></param>
        /// <param name="resourceKey"></param>
        /// <param name="newValue"></param>
        public void UpdateResourceString(Guid languageId, string resourceKey, string newValue)
        {
            // Get the resource
            var localeStringResource = GetResource(languageId, resourceKey);

            if (localeStringResource == null)
            {
                throw new ApplicationException(string.Format("Unable to update resource with key {0} for language {1}. No resource found.", resourceKey, languageId));
            }

            localeStringResource.ResourceValue = newValue;
        }

        /// <summary>
        /// Create a string resource
        /// </summary>
        /// <param name="newLocaleStringResource"></param>
        /// <returns></returns>
        private LocaleStringResource Add(LocaleStringResource newLocaleStringResource)
        {
            return _localizationRepository.Add(newLocaleStringResource);
        }

        /// <summary>
        /// Add a new resource key
        /// </summary>
        /// <param name="newLocaleResourceKey"></param>
        public LocaleResourceKey Add(LocaleResourceKey newLocaleResourceKey)
        {
            // Check to see if a respource key of this name already exists
            var existingResourceKey = _localizationRepository.GetResourceKey(newLocaleResourceKey.Name);

            if (existingResourceKey != null)
            {
                throw new ApplicationException(string.Format("The resource key with name '{0}' already exists.", newLocaleResourceKey.Name));
            }

            newLocaleResourceKey.DateAdded = DateTime.Now;

            // Now add an empty value for each language
            newLocaleResourceKey.LocaleStringResources = new List<LocaleStringResource>();
            foreach (var language in _localizationRepository.GetAll())
            {
                var resourceValue = new LocaleStringResource
                {
                    Id = GuidComb.GenerateComb(),
                    Language = language,
                    LocaleResourceKey = newLocaleResourceKey,
                    ResourceValue = string.Empty
                };

                language.LocaleStringResources.Add(resourceValue);
            }

            // Add the key
            return _localizationRepository.Add(newLocaleResourceKey);
        }

        /// <summary>
        /// Add a new language into the system (does NOT set current language)
        /// </summary>
        /// <param name="language"></param>
        public void Add(Language language)
        {
            // Does the language already exist by name or language-locale?
            var existingLanguage = GetLanguageByLanguageCulture(language.LanguageCulture);

            if (existingLanguage != null)
            {
                throw new LanguageOrCultureAlreadyExistsException(string.Format("There is already a language defined for language-culture '{0}'", existingLanguage.LanguageCulture));
            }

            // Make sure that the new language has a set of empty resources
            language.LocaleStringResources = new List<LocaleStringResource>();
            foreach (var localeResourceKey in _localizationRepository.GetAllResourceKeys())
            {
                var localeStringResource = new LocaleStringResource
                {
                    Id = GuidComb.GenerateComb(),
                    Language = language,
                    LocaleResourceKey = localeResourceKey,
                    ResourceValue = string.Empty
                };
                language.LocaleStringResources.Add(localeStringResource);
            }

            _localizationRepository.Add(language);
        }

        /// <summary>
        /// Add a new language
        /// </summary>
        /// <param name="cultureInfo"></param>
        public Language Add(CultureInfo cultureInfo)
        {
            // Create a domain language object
            var language = new Language
            {
                Name = cultureInfo.EnglishName,
                LanguageCulture = cultureInfo.Name,
            };

            Add(language);

            return language;
        }

        /// <summary>
        /// Get a resource value using a key
        /// </summary>
        /// <param name="languageId"> </param>
        /// <param name="key"></param>
        /// <returns></returns>
        public LocaleStringResource GetResource(Guid languageId, string key)
        {
            try
            {
                return _localizationRepository.GetResource(languageId, key.Trim());
            }
            catch (Exception ex)
            {
                // Could be there is no resource
                _loggingService.Error(string.Format("Unable to retrieve resource key '{0}' for language id {1}. Error: '{2}'.", key, languageId.ToString(), ex.Message));
                return null;
            }
            
        }

        /// <summary>
        /// Get a resource key by id
        /// </summary>
        /// <returns></returns>
        public LocaleStringResource GetResource(Language language, string key)
        {
            return GetResource(language.Id, key);
        }

        /// <summary>
        /// Get a resource key by id
        /// </summary>
        /// <returns></returns>
        public string GetResourceString(Language language, string key)
        {
            var resFormat = GetResource(language.Id, key);
            if (resFormat != null)
            {
                var resValue = resFormat.ResourceValue;
                if (!string.IsNullOrEmpty(resValue))
                {
                    return new LocalizedString(resValue).Text;
                }
            }
            return new LocalizedString(key).Text;
        }

        /// <summary>
        /// Get a resource key by id
        /// </summary>
        /// <returns></returns>
        public string GetResourceString(string key)
        {
            return GetResourceString(CurrentLanguage, key);
        }

        #endregion


        /// <summary>
        /// Update a resource key - change its name
        /// </summary>
        /// <param name="resourceKeyId"></param>
        /// <param name="newName"></param>
        public void UpdateResourceKey(Guid resourceKeyId, string newName)
        {

            // Get the resource
            var localeStringResourceKey = _localizationRepository.GetResourceKey(resourceKeyId);

            if (localeStringResourceKey == null)
            {
                throw new ApplicationException(string.Format("Unable to update resource key {0} . No resource found.", resourceKeyId));
            }

            localeStringResourceKey.Name = StringUtils.SafePlainText(newName);

        }

        /// <summary>
        /// Get all the localization values (cached)
        /// </summary>
        /// <returns></returns>
        public Language CurrentLanguage
        {
            get { return _currentLanguage ?? (_currentLanguage = DefaultLanguage); }

            set { _currentLanguage = value; }
        }

        /// <summary>
        /// The system default language
        /// </summary>
        public Language DefaultLanguage
        {
            get
            {
                var language = _settingsRepository.GetSettings().DefaultLanguage;

                if (language == null)
                {
                    throw new ApplicationException("There is no default language defined in the system.");
                }

                return language;
            }
        }



        /// <summary>
        /// Retrieve a language by the language-culture string e.g. "en-GB"
        /// </summary>
        /// <param name="languageCulture"></param>
        public Language GetLanguageByLanguageCulture(string languageCulture)
        {

            return _localizationRepository.GetLanguageByLanguageCulture(languageCulture);
        }


        /// <summary>
        /// Retrieve a language by name
        /// </summary>
        /// <param name="name"></param>
        public Language GetLanguageByName(string name)
        {

            return _localizationRepository.GetLanguageByLanguageCulture(name);

        }

        /// <summary>
        /// All languages
        /// </summary>
        public IEnumerable<Language> AllLanguages
        {
            get
            {
                return _localizationRepository.GetAll();
            }
        }

        /// <summary>
        /// Get paged set of resources for a language
        /// </summary>
        /// <param name="languageId"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public PagedList<LocaleStringResource> GetAllValues(Guid languageId, int pageIndex, int pageSize)
        {
            return _localizationRepository.GetAllValues(languageId, pageIndex, pageSize);
        }

        /// <summary>
        /// Get resource values for all languages for a key
        /// </summary>
        /// <param name="resourceKeyId"> </param>
        /// <returns></returns>
        public IList<LocaleStringResource> GetAllValuesForKey(Guid resourceKeyId)
        {
            return _localizationRepository.GetAllValuesForKey(resourceKeyId);
        }

        /// <summary>
        /// Get all resource keys - paged
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public PagedList<LocaleResourceKey> GetAllResourceKeys(int pageIndex, int pageSize)
        {
            return _localizationRepository.GetAllResourceKeys(pageIndex, pageSize);
        }

        /// <summary>
        /// Return all the resource keys in the system - non paged
        /// </summary>
        /// <returns></returns>
        public IList<LocaleResourceKey> GetAllResourceKeys()
        {
            return _localizationRepository.GetAllResourceKeys();
        }

        /// <summary>
        /// Search resources in a language
        /// </summary>
        /// <param name="languageId"></param>
        /// <param name="search"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public PagedList<LocaleStringResource> SearchResourceValues(Guid languageId, string search, int pageIndex, int pageSize)
        {
            return _localizationRepository.SearchResourceValues(languageId, StringUtils.SafePlainText(search), pageIndex, pageSize);
        }


        /// <summary>
        /// Search through the resource values
        /// </summary>
        /// <param name="search"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public PagedList<LocaleResourceKey> SearchResourceKeys(string search, int pageIndex, int pageSize)
        {
            return _localizationRepository.SearchResourceKeys(StringUtils.SafePlainText(search), pageIndex, pageSize);
        }

        /// <summary>
        /// Search through the resource values for a language looking
        /// in a subset of keys denoted by the search term
        /// </summary>
        /// <param name="languageId"></param>
        /// <param name="search">The resource value</param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public PagedList<LocaleStringResource> SearchResourceKeys(Guid languageId, string search, int pageIndex, int pageSize)
        {
            return _localizationRepository.SearchResourceKeys(languageId, StringUtils.SafePlainText(search), pageIndex, pageSize);
        }

        /// <summary>
        /// Get an individual language
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Language Get(Guid id)
        {
            return _localizationRepository.Get(id);
        }



        /// <summary>
        /// Delete a language
        /// </summary>
        /// <param name="language"></param>
        public void Delete(Language language)
        {
            // Cannot delete default language
            if (language.Id == DefaultLanguage.Id)
            {
                throw new ApplicationException("Deleting the default language is not allowed.");
            }

            try
            {
                _localizationRepository.Delete(language);
            }
            catch (Exception ex)
            {

                throw new ApplicationException(string.Format("Unable to delete language: {0}", ex.Message), ex);
            }
        }

        /// <summary>
        /// Save language
        /// </summary>
        /// <param name="language"></param>
        public void Save(Language language)
        {
            try
            {
                _localizationRepository.Update(language);
            }
            catch (Exception ex)
            {
                throw new ApplicationException(string.Format("Unable to save language: {0}", ex.Message), ex);
            }

        }

        /// <summary>
        /// Delete a resource key - warning: this will delete all the associated resource strings in all languages
        /// for this key
        /// </summary>
        public void DeleteLocaleResourceKey(LocaleResourceKey resourceKey)
        {
            try
            {
                // Delete the key and its values
                _localizationRepository.DeleteResourceKey(resourceKey);

            }
            catch (Exception ex)
            {
                throw new ApplicationException(string.Format("Unable to delete resource key: {0}", ex.Message), ex);
            }
        }

        /// <summary>
        /// Get a resource key by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public LocaleResourceKey GetResourceKey(Guid id)
        {
            return _localizationRepository.GetResourceKey(id);
        }

        /// <summary>
        /// Creates a new, unsaved resource key, with default (empty) values
        /// </summary>
        /// <returns></returns>
        public LocaleResourceKey CreateEmptyLocaleResourceKey()
        {
            return new LocaleResourceKey
            {
                LocaleStringResources = new List<LocaleStringResource>(),
                Name = string.Empty,
                Notes = string.Empty,
                DateAdded = (DateTime)SqlDateTime.MinValue,
            };
        }

        /// <summary>
        /// Convert a language into CSV format (e.g. for export)
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public string ToCsv(Language language)
        {
            var csv = new StringBuilder();

            foreach (var resource in _localizationRepository.AllLanguageResources(language.Id))
            {
                csv.AppendFormat("{0},{1}", resource.LocaleResourceKey.Name, resource.ResourceValue);
                csv.AppendLine();
            }

            return csv.ToString();
        }

        /// <summary>
        /// Import a language from CSV
        /// </summary>
        /// <param name="langKey"> </param>
        /// <param name="allLines"></param>
        /// <returns>A report on the import</returns>
        public CsvReport FromCsv(string langKey, List<string> allLines)
        {
            var commaSeparator = new[] { ',' };
            var report = new CsvReport();

            if (allLines == null || allLines.Count == 0)
            {
                report.Errors.Add(new CsvErrorWarning
                {
                    ErrorWarningType = CsvErrorWarningType.BadDataFormat,
                    Message = "No language keys or values found."
                });
                return report;
            }

            // Look up the language and culture
            Language language;
            try
            {
                var cultureInfo = LanguageUtils.GetCulture(langKey);

                if (cultureInfo == null)
                {
                    report.Errors.Add(new CsvErrorWarning
                    {
                        ErrorWarningType = CsvErrorWarningType.DoesNotExist,
                        Message = string.Format("The language culture '{0}' does not exist.", langKey)
                    });

                    return report;
                }

                language = Add(cultureInfo);
            }
            catch (LanguageOrCultureAlreadyExistsException ex)
            {
                report.Errors.Add(new CsvErrorWarning { ErrorWarningType = CsvErrorWarningType.AlreadyExists, Message = ex.Message });
                return report;
            }
            catch (Exception ex)
            {
                report.Errors.Add(new CsvErrorWarning { ErrorWarningType = CsvErrorWarningType.ItemBad, Message = ex.Message });
                return report;
            }
            try
            {
                var lineCounter = 0;
                foreach (var line in allLines)
                {
                    lineCounter++;

                    var keyValuePair = line.Split(commaSeparator);

                    if (keyValuePair.Length < 2)
                    {
                        report.Errors.Add(new CsvErrorWarning
                        {
                            ErrorWarningType = CsvErrorWarningType.MissingKeyOrValue,
                            Message = string.Format("Line {0}: a key and a value are required.", lineCounter)
                        });

                        continue;
                    }

                    var key = keyValuePair[0];

                    if (string.IsNullOrEmpty(key))
                    {
                        // Ignore empty keys
                        continue;
                    }

                    var value = keyValuePair[1];

                    var resourceKey = _localizationRepository.GetResourceKey(key);

                    if (language == null)
                    {
                        throw new ApplicationException(string.Format("Unable to create language"));
                    }

                    // If key does not exist, it is a new one to be created
                    if (resourceKey == null)
                    {
                        resourceKey = new LocaleResourceKey
                                          {
                                              Id = GuidComb.GenerateComb(),
                                              Name = key,
                                              DateAdded = DateTime.Now,
                                          };

                        Add(resourceKey);
                        report.Warnings.Add(new CsvErrorWarning
                        {
                            ErrorWarningType = CsvErrorWarningType.NewKeyCreated,
                            Message = string.Format("A new key named '{0}' has been created, and will require a value in all languages.", key)
                        });
                    }

                    // In the new language (only) set the value for the resource
                    foreach (var res in language.LocaleStringResources.Where(res => res.LocaleResourceKey.Name == resourceKey.Name))
                    {
                        res.ResourceValue = value;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                report.Errors.Add(new CsvErrorWarning { ErrorWarningType = CsvErrorWarningType.GeneralError, Message = ex.Message });
            }


            return report;
        }

        /// <summary>
        /// Finds all languages in database and returns them as culture info objects
        /// </summary>
        public IList<CultureInfo> LanguagesInDb
        {
            get
            {
                return AllLanguages.Select(language => LanguageUtils.GetCulture(language.LanguageCulture)).OrderBy(info => info.EnglishName).ToList();
            }
        }

        /// <summary>
        /// Finds all languages NOT in database and returns them as culture info objects
        /// </summary>
        public IList<CultureInfo> LanguagesNotInDb
        {
            get
            {
                var allLanguagesNotInDb = new List<CultureInfo>();
                var allLanguagesInDb = AllLanguages;

                foreach (var cultureInfo in LanguageUtils.AllCultures)
                {
                    var found = allLanguagesInDb.Any(language => language.LanguageCulture == cultureInfo.Name);
                    if (!found)
                    {
                        allLanguagesNotInDb.Add(cultureInfo);
                    }

                }

                return allLanguagesNotInDb.OrderBy(info => info.EnglishName).ToList();
            }
        }


    }

    public class LanguageOrCultureAlreadyExistsException : Exception
    {
        public LanguageOrCultureAlreadyExistsException(string message)
            : base(message)
        {

        }
    }
}
