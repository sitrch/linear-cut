using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using LinearCutWpf.Models;
using LinearCutWpf.Services;

namespace LinearCutWpf.Tests
{
    [Collection("Sequential")]
    public class ProfileHeightServiceAdditionalTests : IDisposable
    {
        private readonly string _testFilePath;
        
        public ProfileHeightServiceAdditionalTests()
        {
            _testFilePath = Path.GetTempFileName();
            // Переопределяем путь к файлу для тестов
            ProfileHeightService.OverrideFilePath = _testFilePath;
        }
        
        public void Dispose()
        {
            // Удаляем тестовый файл после тестов
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
            // Сбрасываем переопределение пути
            ProfileHeightService.OverrideFilePath = null;
        }
        
        [Fact]
        public void SaveAndLoadProfileHeightsWithMetadata_HandlesNullValuesCorrectly()
        {
            // Arrange
            var testRows = new List<ArticleGroupingRow>
            {
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART001", 
                    SelectedVisibleHeight = 1200,
                    IsDefaultValue = false,
                    IsManuallyChanged = true
                },
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART002", 
                    SelectedVisibleHeight = null,
                    IsDefaultValue = true,
                    IsManuallyChanged = false
                },
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART003", 
                    SelectedVisibleHeight = 1000,
                    IsDefaultValue = false,
                    IsManuallyChanged = false
                }
            };
            
            double? defaultHeight = 1000;
            
            // Act
            ProfileHeightService.SaveProfileHeightsWithMetadata(testRows, defaultHeight);
            
            // Assert
            var loadedData = ProfileHeightService.LoadProfileHeightsWithMetadata();
            
            Assert.NotNull(loadedData);
            Assert.Equal(3, loadedData.Count); // Все три записи должны сохраниться
            
            Assert.True(loadedData.ContainsKey("ART001"));
            var art001Data = loadedData["ART001"];
            Assert.Equal(1200, art001Data.height);
            Assert.True(art001Data.isManuallyChanged);
            Assert.False(art001Data.isDefaultValue);
            
            Assert.True(loadedData.ContainsKey("ART002"));
            var art002Data = loadedData["ART002"];
            Assert.Null(art002Data.height);
            Assert.False(art002Data.isManuallyChanged);
            Assert.True(art002Data.isDefaultValue);
            
            Assert.True(loadedData.ContainsKey("ART003"));
            var art003Data = loadedData["ART003"];
            Assert.Equal(1000, art003Data.height);
            Assert.False(art003Data.isManuallyChanged);
            Assert.False(art003Data.isDefaultValue);
            
            var loadedDefaultHeight = ProfileHeightService.LoadDefaultHeight();
            Assert.Equal(defaultHeight, loadedDefaultHeight);
        }
        
        [Fact]
        public void SaveAndLoadProfileHeightsWithMetadata_PreservesDefaultValueFlagCorrectly()
        {
            // Arrange
            var testRows = new List<ArticleGroupingRow>
            {
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART001", 
                    SelectedVisibleHeight = 1200,
                    IsDefaultValue = false,
                    IsManuallyChanged = true
                },
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART002", 
                    SelectedVisibleHeight = 1000,
                    IsDefaultValue = true,
                    IsManuallyChanged = false
                }
            };
            
            double? defaultHeight = 1000;
            
            // Act
            ProfileHeightService.SaveProfileHeightsWithMetadata(testRows, defaultHeight);
            
            // Assert
            var loadedData = ProfileHeightService.LoadProfileHeightsWithMetadata();
            
            Assert.NotNull(loadedData);
            Assert.Equal(2, loadedData.Count);
            
            // ART001 должен сохранить флаги правильно
            Assert.True(loadedData.ContainsKey("ART001"));
            var art001Data = loadedData["ART001"];
            Assert.Equal(1200, art001Data.height);
            Assert.True(art001Data.isManuallyChanged);
            Assert.False(art001Data.isDefaultValue);
            
            // ART002 должен сохранить флаги правильно
            Assert.True(loadedData.ContainsKey("ART002"));
            var art002Data = loadedData["ART002"];
            Assert.Equal(1000, art002Data.height);
            Assert.True(art002Data.isDefaultValue);
            Assert.False(art002Data.isManuallyChanged);
        }
        
        [Fact]
        public void SaveAndLoadProfileHeightsWithMetadata_HandlesEmptyArticleNames()
        {
            // Arrange
            var testRows = new List<ArticleGroupingRow>
            {
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART001", 
                    SelectedVisibleHeight = 1200
                },
                new ArticleGroupingRow 
                { 
                    ArticleName = "", 
                    SelectedVisibleHeight = 1000
                },
                new ArticleGroupingRow 
                { 
                    ArticleName = null, 
                    SelectedVisibleHeight = 1500
                }
            };
            
            double? defaultHeight = 1000;
            
            // Act
            ProfileHeightService.SaveProfileHeightsWithMetadata(testRows, defaultHeight);
            
            // Assert
            var loadedData = ProfileHeightService.LoadProfileHeightsWithMetadata();
            
            Assert.NotNull(loadedData);
            Assert.Single(loadedData); // Только ART001 должен сохраниться
            Assert.True(loadedData.ContainsKey("ART001"));
            
            var art001Data = loadedData["ART001"];
            Assert.Equal(1200, art001Data.height);
        }
        
        [Fact]
        public void SaveProfileHeightsWithMetadata_DoesNotOverwriteExistingData_WhenNoNewData()
        {
            // Arrange - сначала сохраним некоторые данные
            var initialRows = new List<ArticleGroupingRow>
            {
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART001", 
                    SelectedVisibleHeight = 1200,
                    IsDefaultValue = false,
                    IsManuallyChanged = true
                }
            };
            
            double? defaultHeight = 1000;
            ProfileHeightService.SaveProfileHeightsWithMetadata(initialRows, defaultHeight);
            
            // Сохраняем время файла ДО попытки сохранения пустых данных
            var fileInfoBefore = new FileInfo(_testFilePath);
            var fileTimeBefore = fileInfoBefore.LastWriteTime;
            
            // Act - попытаемся сохранить пустые данные
            var emptyRows = new List<ArticleGroupingRow>();
            ProfileHeightService.SaveProfileHeightsWithMetadata(emptyRows, defaultHeight);
            
            // Мы сохранили данные с пустым списком строк, но с defaultHeight. 
            // Файл мог быть перезаписан, но старые профили не должны исчезнуть.
            
            // Assert - данные должны остаться
            var loadedData = ProfileHeightService.LoadProfileHeightsWithMetadata();
            Assert.NotNull(loadedData);
            Assert.Single(loadedData);
            Assert.True(loadedData.ContainsKey("ART001"));
            var art001Data = loadedData["ART001"];
            Assert.Equal(1200, art001Data.height);
            Assert.True(art001Data.isManuallyChanged);
            Assert.False(art001Data.isDefaultValue);
        }
    }
}