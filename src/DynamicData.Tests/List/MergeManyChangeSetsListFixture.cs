﻿using System;
using System.Linq;
using System.Reactive.Linq;
using Bogus;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.List;

public sealed class MergeManyChangeSetsListFixture : IDisposable
{
#if DEBUG
    const int InitialOwnerCount = 7;
    const int AddRangeSize = 5;
    const int RemoveRangeSize = 3;
#else
    const int InitialOwnerCount = 103;
    const int AddRangeSize = 53;
    const int RemoveRangeSize = 37;
#endif

    private readonly ISourceList<AnimalOwner> _animalOwners = new SourceList<AnimalOwner>();
    private readonly ChangeSetAggregator<AnimalOwner> _animalOwnerResults;
    private readonly ChangeSetAggregator<Animal> _animalResults;
    private readonly Randomizer _randomizer;

    public MergeManyChangeSetsListFixture()
    {
        Randomizer.Seed = new Random(0x12291977);
        _randomizer = new Randomizer();
        _animalOwners.AddRange(Fakers.AnimalOwner.Generate(InitialOwnerCount));

        _animalOwnerResults = _animalOwners.Connect().AsAggregator();
        _animalResults = _animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();
    }

    [Fact]
    public void NullChecks()
    {
        // Arrange
        var emptyChangeSetObs = Observable.Empty<IChangeSet<int>>();
        var nullChangeSetObs = (IObservable<IChangeSet<int>>)null!;
        var emptySelector = new Func<int, IObservable<IChangeSet<string>>>(i => Observable.Empty<IChangeSet<string>>());
        var nullSelector = (Func<int, IObservable<IChangeSet<string>>>)null!;

        // Act
        var checkParam1 = () => nullChangeSetObs.MergeManyChangeSets(emptySelector);
        var checkParam2 = () => emptyChangeSetObs.MergeManyChangeSets(nullSelector);

        // Assert
        emptyChangeSetObs.Should().NotBeNull();
        emptySelector.Should().NotBeNull();
        nullChangeSetObs.Should().BeNull();
        nullSelector.Should().BeNull();

        checkParam1.Should().Throw<ArgumentNullException>();
        checkParam2.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ResultContainsAllInitialChildren()
    {
        // Arrange

        // Act

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount);
        CheckResultContents();
    }

    [Fact]
    public void ResultContainsChildrenFromParentsAddedWithAddRange()
    {
        // Arrange
        var addThese = Fakers.AnimalOwner.Generate(AddRangeSize);

        // Act
        _animalOwners.AddRange(addThese);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount + AddRangeSize);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + AddRangeSize);
        addThese.SelectMany(added => added.Animals.Items).ForEach(added => _animalResults.Data.Items.Should().Contain(added));
        CheckResultContents();
    }

    [Fact]
    public void ResultContainsChildrenFromParentsAddedWithAdd()
    {
        // Arrange
        var addThis = Fakers.AnimalOwner.Generate();

        // Act
        _animalOwners.Add(addThis);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount + 1);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
        addThis.Animals.Items.ForEach(added => _animalResults.Data.Items.Should().Contain(added));
        CheckResultContents();
    }

    [Fact]
    public void ResultContainsChildrenFromParentsAddedWithInsert()
    {
        // Arrange
        var insertIndex = _randomizer.Number(_animalOwners.Count);
        var insertThis = Fakers.AnimalOwner.Generate();

        // Act
        _animalOwners.Insert(insertIndex, insertThis);

        // Assert
        _animalOwners.Items.ElementAt(insertIndex).Should().Be(insertThis);
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount + 1);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
        insertThis.Animals.Items.ForEach(added => _animalResults.Data.Items.Should().Contain(added));
        CheckResultContents();
    }

    [Fact]
    public void ResultDoesNotContainChildrenFromParentsRemovedWithRemove()
    {
        // Arrange
        var removeThis = _randomizer.ListItem(_animalOwners.Items.ToList());

        // Act
        _animalOwners.Remove(removeThis);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount - 1);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
        removeThis.Animals.Items.ForEach(removed => _animalResults.Data.Items.Should().NotContain(removed));
        CheckResultContents();
        removeThis.Dispose();
    }

    [Fact]
    public void ResultDoesNotContainChildrenFromParentsRemovedWithRemoveAt()
    {
        // Arrange
        var removeIndex = _randomizer.Number(_animalOwners.Count - 1);
        var removeThis = _animalOwners.Items.ElementAt(removeIndex);

        // Act
        _animalOwners.RemoveAt(removeIndex);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount - 1);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
        removeThis.Animals.Items.ForEach(removed => _animalResults.Data.Items.Should().NotContain(removed));
        CheckResultContents();
        removeThis.Dispose();
    }

    [Fact]
    public void ResultDoesNotContainChildrenFromParentsRemovedWithRemoveRange()
    {
        // Arrange
        var removeIndex = _randomizer.Number(_animalOwners.Count - RemoveRangeSize - 1);
        var removeThese = _animalOwners.Items.Skip(removeIndex).Take(RemoveRangeSize);

        // Act
        _animalOwners.RemoveRange(removeIndex, RemoveRangeSize);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount - RemoveRangeSize);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + RemoveRangeSize);
        removeThese.SelectMany(owner => owner.Animals.Items).ForEach(removed => _animalResults.Data.Items.Should().NotContain(removed));
        CheckResultContents();
        removeThese.ForEach(owner => owner.Dispose());
    }

    [Fact]
    public void ResultDoesNotContainChildrenFromParentsRemovedWithRemoveMany()
    {
        // Arrange
        var removeThese = _randomizer.ListItems(_animalOwners.Items.ToList(), RemoveRangeSize);

        // Act
        _animalOwners.RemoveMany(removeThese);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount - RemoveRangeSize);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + RemoveRangeSize);
        removeThese.SelectMany(owner => owner.Animals.Items).ForEach(removed => _animalResults.Data.Items.Should().NotContain(removed));
        CheckResultContents();
        removeThese.ForEach(owner => owner.Dispose());
    }

    [Fact]
    public void ResultContainsCorrectItemsAfterParentReplacement()
    {
        // Arrange
        var replaceThis = _randomizer.ListItem(_animalOwners.Items.ToList());
        var withThis = Fakers.AnimalOwner.Generate();

        // Act
        _animalOwners.Replace(replaceThis, withThis);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount); // Owner Count should not change
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 2); // +2 = 1 Message removing animals from old value, +1 message adding from new value
        replaceThis.Animals.Items.ForEach(removed => _animalResults.Data.Items.Should().NotContain(removed));
        withThis.Animals.Items.ForEach(added => _animalResults.Data.Items.Should().Contain(added));
        CheckResultContents();
        replaceThis.Dispose();
    }

    [Fact]
    public void ResultEmptyIfSourceIsCleared()
    {
        // Arrange
        var items = _animalOwners.Items.ToList();

        // Act
        _animalOwners.Clear();

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(0);
        _animalResults.Data.Count.Should().Be(0);
        CheckResultContents();
        items.ForEach(owner => owner.Dispose());
    }

    [Fact]
    public void ResultContainsChildrenAddedWithAddRange()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var addThese = Fakers.Animal.Generate(AddRangeSize);
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);

        // Act
        randomOwner.Animals.AddRange(addThese);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
        addThese.ForEach(animal => _animalResults.Data.Items.Should().Contain(animal));
        _animalOwners.Items.Sum(owner => owner.Animals.Count).Should().Be(initialCount + AddRangeSize);
        CheckResultContents();
    }

    [Fact]
    public void ResultContainsChildrenAddedWithAdd()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var addThis = Fakers.Animal.Generate();
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);

        // Act
        randomOwner.Animals.Add(addThis);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
        _animalResults.Data.Items.Should().Contain(addThis);
        _animalOwners.Items.Sum(owner => owner.Animals.Count).Should().Be(initialCount + 1);
        CheckResultContents();
    }

    [Fact]
    public void ResultContainsChildrenAddedWithInsert()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var insertIndex = _randomizer.Number(randomOwner.Animals.Items.Count());
        var insertThis = Fakers.Animal.Generate();
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);

        // Act
        randomOwner.Animals.Insert(insertIndex, insertThis);

        // Assert
        randomOwner.Animals.Items.ElementAt(insertIndex).Should().Be(insertThis);
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
        _animalResults.Data.Items.Should().Contain(insertThis);
        _animalOwners.Items.Sum(owner => owner.Animals.Count).Should().Be(initialCount + 1);
        CheckResultContents();
    }

    [Fact]
    public void ResultDoesNotContainChildrenRemovedWithRemove()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var removeThis = _randomizer.ListItem(randomOwner.Animals.Items.ToList());
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);

        // Act
        randomOwner.Animals.Remove(removeThis);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
        _animalResults.Data.Items.Should().NotContain(removeThis);
        _animalOwners.Items.Sum(owner => owner.Animals.Count).Should().Be(initialCount - 1);
        CheckResultContents();
    }

    [Fact]
    public void ResultDoesNotContainChildrenRemovedWithRemoveAt()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var removeIndex = _randomizer.Number(randomOwner.Animals.Count - 1);
        var removeThis = randomOwner.Animals.Items.ElementAt(removeIndex);
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);

        // Act
        randomOwner.Animals.RemoveAt(removeIndex);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
        _animalResults.Data.Items.Should().NotContain(removeThis);
        _animalOwners.Items.Sum(owner => owner.Animals.Count).Should().Be(initialCount - 1);
        CheckResultContents();
    }

    [Fact]
    public void ResultDoesNotContainChildrenRemovedWithRemoveRange()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var removeCount = _randomizer.Number(1, randomOwner.Animals.Count - 1);
        var removeIndex = _randomizer.Number(randomOwner.Animals.Count - removeCount - 1);
        var removeThese = randomOwner.Animals.Items.Skip(removeIndex).Take(removeCount);

        // Act
        randomOwner.Animals.RemoveRange(removeIndex, removeCount);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
        removeThese.ForEach(removed => randomOwner.Animals.Items.Should().NotContain(removed));
        CheckResultContents();
    }

    [Fact]
    public void ResultDoesNotContainChildrenRemovedWithRemoveMany()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var removeCount = _randomizer.Number(1, randomOwner.Animals.Count - 1);
        var removeThese = _randomizer.ListItems(randomOwner.Animals.Items.ToList(), removeCount);

        // Act
        randomOwner.Animals.RemoveMany(removeThese);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
        removeThese.ForEach(removed => randomOwner.Animals.Items.Should().NotContain(removed));
        CheckResultContents();
    }

    [Fact]
    public void ResultContainsCorrectItemsAfterChildReplacement()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var replaceThis = _randomizer.ListItem(randomOwner.Animals.Items.ToList());
        var withThis = Fakers.Animal.Generate();

        // Act
        randomOwner.Animals.Replace(replaceThis, withThis);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
        randomOwner.Animals.Items.Should().NotContain(replaceThis);
        randomOwner.Animals.Items.Should().Contain(withThis);
        CheckResultContents();
    }

    [Fact]
    public void ResultContainsCorrectItemsAfterChildClear()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var removedAnimals = randomOwner.Animals.Items.ToList();

        // Act
        randomOwner.Animals.Clear();

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
        randomOwner.Animals.Count.Should().Be(0);
        removedAnimals.ForEach(removed => _animalResults.Data.Items.Should().NotContain(removed));
        CheckResultContents();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ResultCompletesOnlyWhenSourceAndAllChildrenComplete(bool completeSource, bool completeChildren)
    {
        // Arrange

        // Act
        _animalOwners.Items.Skip(completeChildren ? 0 : 1).ForEach(owner => owner.Dispose());
        if (completeSource)
        {
            _animalOwners.Dispose();
        }

        // Assert
        _animalOwnerResults.IsCompleted.Should().Be(completeSource);
        _animalResults.IsCompleted.Should().Be(completeSource && completeChildren);
    }

    [Fact]
    public void ResultFailsIfSourceFails()
    {
        // Arrange
        var expectedError = new Exception("Expected");
        var throwObservable = Observable.Throw<IChangeSet<AnimalOwner>>(expectedError);
        using var results = _animalOwners.Connect().Concat(throwObservable).MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

        // Act
        _animalOwners.Dispose();

        // Assert
        results.Exception.Should().Be(expectedError);
    }

    private void CheckResultContents()
    {
        var expectedOwners = _animalOwners.Items.ToList();
        var expectedAnimals = expectedOwners.SelectMany(owner => owner.Animals.Items).ToList();

        // These should be subsets of each other, so check one subset and the size
        expectedOwners.Should().BeSubsetOf(_animalOwnerResults.Data.Items);
        _animalOwnerResults.Data.Items.Count().Should().Be(expectedOwners.Count);

        // These should be subsets of each other, so check one subset and the size
        expectedAnimals.Should().BeSubsetOf(_animalResults.Data.Items);
        _animalResults.Data.Items.Count().Should().Be(expectedAnimals.Count);
    }

    public void Dispose()
    {
        _animalOwners.Items.ForEach(owner => owner.Dispose());
        _animalOwnerResults.Dispose();
        _animalResults.Dispose();
        _animalOwners.Dispose();
    }
}
