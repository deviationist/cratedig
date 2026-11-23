import {
  addResponseChunk,
  emptyResponseBuffer,
  filterResponse,
  getResponses,
  parseFiltersFromString,
} from '../../../lib/searches';
import ErrorSegment from '../../Shared/ErrorSegment';
import LoaderSegment from '../../Shared/LoaderSegment';
import Switch from '../../Shared/Switch';
import Response from '../Response';
import SearchDetailHeader from './SearchDetailHeader';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  Button,
  Checkbox,
  Dropdown,
  Icon,
  Input,
  Segment,
} from 'semantic-ui-react';

const sortDropdownOptions = [
  {
    key: 'uploadSpeed',
    text: 'Upload Speed (Fastest to Slowest)',
    value: 'uploadSpeed',
  },
  {
    key: 'queueLength',
    text: 'Queue Depth (Least to Most)',
    value: 'queueLength',
  },
];

// the line shown while a search runs
const describeProgress = ({
  fileCount,
  lockedFileCount,
  responseCount,
  state,
}) =>
  state === 'InProgress'
    ? `Found ${fileCount} files ${
        lockedFileCount > 0 ? `(plus ${lockedFileCount} locked) ` : ''
      }from ${responseCount} users`
    : 'Loading results...';

const SearchDetail = ({
  creating,
  disabled,
  hub,
  onCreate,
  onRemove,
  onStop,
  removing,
  search,
  stopping,
}) => {
  const { id, isComplete } = search;

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(undefined);

  const [results, setResults] = useState([]);
  // the responses streamed so far, and any chunk waiting for a gap to fill
  const streamRef = useRef(emptyResponseBuffer);

  // filters and sorting options
  const [hiddenResults, setHiddenResults] = useState([]);
  const [resultSort, setResultSort] = useState('uploadSpeed');
  const [hideLocked, setHideLocked] = useState(true);
  const [hideNoFreeSlots, setHideNoFreeSlots] = useState(false);
  const [foldResults, setFoldResults] = useState(false);
  const [resultFilters, setResultFilters] = useState('');
  const [displayCount, setDisplayCount] = useState(5);

  // while the search runs, show its responses as they arrive: subscribe to
  // them over the hub, which answers with those received so far and then
  // sends the rest in numbered chunks
  useEffect(() => {
    if (isComplete || !hub) {
      return undefined;
    }

    let active = true;
    streamRef.current = emptyResponseBuffer;

    const apply = (chunk) => {
      if (!active || !chunk || chunk.searchId !== id) return;

      streamRef.current = addResponseChunk(streamRef.current, chunk);
      setResults(streamRef.current.responses);
    };

    const subscribe = async () => {
      try {
        apply(await hub.invoke('Subscribe', id));
      } catch (subscribeError) {
        // not fatal: the results load in full once the search completes
        console.warn('could not subscribe to search responses', subscribeError);
      }
    };

    const unsubscribe = async () => {
      try {
        await hub.invoke('Unsubscribe', id);
      } catch {
        // the connection is gone, and the subscription with it
      }
    };

    hub.on('responses', apply);
    subscribe();

    return () => {
      active = false;
      hub.off('responses', apply);
      unsubscribe();
    };
  }, [hub, id, isComplete]);

  // once the search is complete, fetch the full set.  it replaces whatever was
  // streamed without a loader, since it is the same set plus anything a dropped
  // chunk missed; a search opened after it completed shows the loader
  useEffect(() => {
    if (!isComplete) {
      return undefined;
    }

    let active = true;
    const streamed = streamRef.current.responses.length > 0;

    const get = async () => {
      try {
        if (!streamed) setLoading(true);

        const responses = await getResponses({ id });

        if (active) {
          setResults(responses);
          setLoading(false);
        }
      } catch (getError) {
        if (active) {
          setError(getError);
          setLoading(false);
        }
      }
    };

    get();

    return () => {
      active = false;
    };
  }, [id, isComplete]);

  // apply sorting and filters.  this can take a while for larger result
  // sets, so memoize it.
  const sortedAndFilteredResults = useMemo(() => {
    const sortOptions = {
      queueLength: { field: 'queueLength', order: 'asc' },
      uploadSpeed: { field: 'uploadSpeed', order: 'desc' },
    };

    const { field, order } = sortOptions[resultSort];

    const filters = parseFiltersFromString(resultFilters);

    return results
      .filter((r) => !hiddenResults.includes(r.username))
      .map((r) => {
        if (hideLocked) {
          return { ...r, lockedFileCount: 0, lockedFiles: [] };
        }

        return r;
      })
      .map((response) => filterResponse({ filters, response }))
      .filter((r) => r.fileCount + r.lockedFileCount > 0)
      .filter((r) => !(hideNoFreeSlots && !r.hasFreeUploadSlot))
      .sort((a, b) => {
        if (order === 'asc') {
          return a[field] - b[field];
        }

        return b[field] - a[field];
      });
  }, [
    hiddenResults,
    hideLocked,
    hideNoFreeSlots,
    resultFilters,
    resultSort,
    results,
  ]);

  // when a user uses the action buttons, we will *probably* re-use this component,
  // but with a new search ID.  clear everything to prepare for the transition
  const reset = () => {
    setLoading(false);
    setError(undefined);
    setResults([]);
    streamRef.current = emptyResponseBuffer;
    setHiddenResults([]);
    setDisplayCount(5);
  };

  const create = async ({ navigate, search: searchForCreate }) => {
    reset();
    onCreate({ navigate, search: searchForCreate });
  };

  const remove = async () => {
    reset();
    onRemove(search);
  };

  const progress = describeProgress(search);

  const filteredCount = results?.length - sortedAndFilteredResults.length;
  const remainingCount = sortedAndFilteredResults.length - displayCount;
  const loaded = !removing && !creating && !loading && results;

  if (error) {
    return <ErrorSegment caption={error?.message ?? error} />;
  }

  return (
    <>
      <SearchDetailHeader
        creating={creating}
        disabled={disabled}
        loaded={loaded}
        loading={loading}
        onCreate={create}
        onRemove={remove}
        onStop={onStop}
        removing={removing}
        search={search}
        stopping={stopping}
      />
      <Switch
        loading={loading && <LoaderSegment />}
        searching={
          !isComplete &&
          results.length === 0 && <LoaderSegment>{progress}</LoaderSegment>
        }
      >
        {!isComplete && (
          <Segment
            className="search-progress"
            raised
          >
            <Icon
              loading
              name="circle notch"
            />
            {progress}
          </Segment>
        )}
        {loaded && (
          <Segment
            className="search-options"
            raised
          >
            <Dropdown
              button
              className="search-options-sort icon"
              floating
              icon="sort"
              labeled
              onChange={(_event, { value }) => setResultSort(value)}
              options={sortDropdownOptions}
              text={
                sortDropdownOptions.find((o) => o.value === resultSort).text
              }
            />
            <div className="search-option-toggles">
              <Checkbox
                checked={hideLocked}
                className="search-options-hide-locked"
                label="Hide Locked Results"
                onChange={() => setHideLocked(!hideLocked)}
                toggle
              />
              <Checkbox
                checked={hideNoFreeSlots}
                className="search-options-hide-no-slots"
                label="Hide Results with No Free Slots"
                onChange={() => setHideNoFreeSlots(!hideNoFreeSlots)}
                toggle
              />
              <Checkbox
                checked={foldResults}
                className="search-options-fold-results"
                label="Fold Results"
                onChange={() => setFoldResults(!foldResults)}
                toggle
              />
            </div>
            <Input
              action={
                Boolean(resultFilters) && {
                  color: 'red',
                  icon: 'x',
                  onClick: () => setResultFilters(''),
                }
              }
              className="search-filter"
              label={{ content: 'Filter', icon: 'filter' }}
              onChange={(_event, data) => setResultFilters(data.value)}
              placeholder="
                lackluster container -bothersome iscbr|isvbr islossless|islossy 
                minbitrate:320 minbitdepth:24 minfilesize:10 minfilesinfolder:8 minlength:5000
              "
              value={resultFilters}
            />
          </Segment>
        )}
        {loaded &&
          sortedAndFilteredResults.slice(0, displayCount).map((r) => (
            <Response
              disabled={disabled}
              isInitiallyFolded={foldResults}
              key={r.username}
              onHide={() => setHiddenResults([...hiddenResults, r.username])}
              response={r}
            />
          ))}
        {loaded &&
          (remainingCount > 0 ? (
            <Button
              className="showmore-button"
              fluid
              onClick={() => setDisplayCount(displayCount + 5)}
              primary
              size="large"
            >
              Show {remainingCount > 5 ? 5 : remainingCount} More Results{' '}
              {`(${remainingCount} remaining, ${filteredCount} hidden by filter(s))`}
            </Button>
          ) : filteredCount > 0 ? (
            <Button
              className="showmore-button"
              disabled
              fluid
              size="large"
            >{`All results shown. ${filteredCount} results hidden by filter(s)`}</Button>
          ) : (
            ''
          ))}
      </Switch>
    </>
  );
};

export default SearchDetail;
