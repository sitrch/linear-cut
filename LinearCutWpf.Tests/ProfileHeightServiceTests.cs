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
    public class ProfileHeightServiceTests : IDisposable
    {
        private readonly string _testFilePath;
        
        public ProfileHeightServiceTests()
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
        public void SaveAndLoadProfileHeightsWithMetadata_WorksCorrectly()
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
                    ArticleName = "ART002", 
                    SelectedVisibleHeight = 1000
                },
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART003", 
                    SelectedVisibleHeight = null
                },
                new ArticleGroupingRow 
                { 
                    ArticleName = "", 
                    SelectedVisibleHeight = 1500
                }
            };
            
            double? defaultHeight = 1000;
            
            // Act
            ProfileHeightService.SaveProfileHeightsWithMetadata(testRows, defaultHeight);
            
            // Assert
            var loadedData = ProfileHeightService.LoadProfileHeightsWithMetadata();
            
            Assert.NotNull(loadedData);
            Assert.Equal(2, loadedData.Count); // Только артикулы с высотой
            
            Assert.True(loadedData.ContainsKey("ART001"));
            Assert.True(loadedData.ContainsKey("ART002"));
            Assert.False(loadedData.ContainsKey("ART003")); // Не должен сохраниться
            Assert.False(loadedData.ContainsKey("")); // Не должен сохраниться
            
             var art001Data = loadedData["ART001"];
             Assert.Equal(1200, art001Data.height);
             Assert.False(art001Data.isDefaultValue);
             Assert.False(art001Data.isManuallyChanged);
             
             var art002Data = loadedData["ART002"];
             Assert.Equal(1000, art002Data.height);
             Assert.False(art002Data.isDefaultValue);
             Assert.False(art002Data.isManuallyChanged);
             
             var loadedDefaultHeight = ProfileHeightService.LoadDefaultHeight();
             Assert.Equal(defaultHeight, loadedDefaultHeight);
        }
        
        [Fact]
        public void LoadProfileHeightsWithMetadata_ReturnsEmptyDictionary_WhenFileNotExists()
        {
            // Arrange
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
            
            // Act
            var result = ProfileHeightService.LoadProfileHeightsWithMetadata();
            
            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }
        
        [Fact]
        public void LoadDefaultHeight_ReturnsNull_WhenFileNotExists()
        {
            // Arrange
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
            
            // Act
            var result = ProfileHeightService.LoadDefaultHeight();
            
            // Assert
            Assert.Null(result);
        }
        
        [Fact]
        public void SaveProfileHeightsWithMetadata_DoesNotCreateFile_WhenNoDataToSave()
        {
            // Arrange
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
            
            var emptyRows = new List<ArticleGroupingRow>();
            double? defaultHeight = null;
            
            // Act
            ProfileHeightService.SaveProfileHeightsWithMetadata(emptyRows, defaultHeight);
            
            // Assert
            Assert.False(File.Exists(_testFilePath), "Файл не должен создаваться, когда нет данных для сохранения");
        }
        
        [Fact]
        public void SaveAndLoadProfileHeightsWithMetadata_PreservesMetadata()
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
            
            Assert.True(loadedData.ContainsKey("ART001"));
            var art001Data = loadedData["ART001"];
            Assert.Equal(1200, art001Data.height);
            Assert.True(art001Data.isManuallyChanged);
            Assert.False(art001Data.isDefaultValue);
            
            Assert.True(loadedData.ContainsKey("ART002"));
            var art002Data = loadedData["ART002"];
            Assert.Equal(1000, art002Data.height);
            Assert.True(art002Data.isDefaultValue);
            Assert.False(art002Data.isManuallyChanged);
            
            var loadedDefaultHeight = ProfileHeightService.LoadDefaultHeight();
            Assert.Equal(defaultHeight, loadedDefaultHeight);
        }
    }
}