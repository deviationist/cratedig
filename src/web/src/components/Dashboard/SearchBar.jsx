import '../Search/Search.css';
import { urlBase } from '../../config';
import { createEnterToSearch } from '../../lib/searchEnter';
import * as library from '../../lib/searches';
import * as watchLibrary from '../../lib/watches';
import WatchModal from '../Search/WatchModal';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useHistory } from 'react-router-dom';
import { toast } from 'react-toastify';
import { Button, Icon, Input, Popup, Segment } from 'semantic-ui-react';
import { v4 as uuidv4 } from 'uuid';

const SearchBar = ({ server } = {}) => {
  const [creating, setCreating] = useState(false);
  const [watchDraft, setWatchDraft] = useState(undefined);

  const inputRef = useRef();
  // the "started" toast of the last search, taken down if a second Enter opens it
  const toastRef = useRef();
  const history = useHistory();

  const openSearch = (id) => {
    toast.dismiss(toastRef.current);
    history.push(`${urlBase}/searches/${id}`);
  };

  // resolves to the new search's id, or undefined when it could not be created
  const create = async ({ navigate = false, search } = {}) => {
    const ref = inputRef?.current?.inputRef?.current;
    const searchText = search || ref.value;
    const id = uuidv4();

    try {
      setCreating(true);
      await library.create({ id, searchText });

      ref.value = '';
      ref.focus();

      setCreating(false);

      if (navigate) {
        openSearch(id);
      } else {
        const label =
          searchText.length > 30 ? `${searchText.slice(0, 15)}...` : searchText;
        toastRef.current = toast.info(
          <span>
            Search for &lsquo;{label}&rsquo; started.{' '}
            <Link to={`${urlBase}/searches/${id}`}>View results</Link>
          </span>,
        );
      }

      return id;
    } catch (createError) {
      console.error(createError);
      toast.error(
        createError?.response?.data ?? createError?.message ?? createError,
      );
      setCreating(false);
      return undefined;
    }
  };

  // Enter once searches and stays, twice opens it (lib/searchEnter). Made once,
  // since it remembers the last Enter; it reaches the current render's
  // functions through a ref
  const latestRef = useRef();
  latestRef.current = { create, openSearch };
  const onEnter = useMemo(
    () =>
      createEnterToSearch({
        create: (search) => latestRef.current.create({ search }),
        open: (id) => latestRef.current.openSearch(id),
      }),
    [],
  );

  const createWatch = async (watch) => {
    const ref = inputRef?.current?.inputRef?.current;
    const searchText = watchDraft?.searchText ?? ref?.value;

    try {
      const { id } = await watchLibrary.createWatchedSearch({
        searchText,
        watch,
      });

      setWatchDraft(undefined);

      try {
        ref.value = '';
      } catch {
        // the input is not mounted; nothing to clear
      }

      toast.success(
        <span>
          Watching &lsquo;{searchText}&rsquo;.{' '}
          <Link to={`${urlBase}/searches/${id}`}>View</Link>
        </span>,
      );
    } catch (watchError) {
      console.error(watchError);
      toast.error(
        watchError?.response?.data ?? watchError?.message ?? watchError,
      );
    }
  };

  useEffect(() => {
    inputRef?.current?.inputRef?.current?.focus();
  }, []);

  return (
    <Segment
      className="search-segment"
      raised
    >
      <div className="search-segment-icon">
        <Icon
          name="search"
          size="big"
        />
      </div>
      <Input
        action={
          <>
            <Button
              disabled={creating || !server?.isConnected}
              icon="plus"
              onClick={create}
            />
            <Popup
              content="Re-run this search on a schedule and email me what is new"
              position="bottom center"
              trigger={
                <Button
                  disabled={creating || !server?.isConnected}
                  icon="clock outline"
                  onClick={() => {
                    const searchText =
                      inputRef?.current?.inputRef?.current?.value ?? '';
                    const validation = library.validateSearchText(searchText);

                    if (!validation.ok) {
                      toast.error(validation.reason);
                      return;
                    }

                    setWatchDraft({ searchText });
                  }}
                />
              }
            />
            <Button
              disabled={creating || !server?.isConnected}
              icon="search"
              onClick={() => create({ navigate: true })}
            />
          </>
        }
        className="search-input"
        // not disabled while creating: a disabled field loses focus, and
        // the second Enter of a double Enter would land nowhere
        disabled={!server?.isConnected}
        input={
          <input
            data-lpignore="true"
            placeholder={
              server?.isConnected
                ? 'Search phrase'
                : 'Connect to server to perform a search'
            }
            type="search"
          />
        }
        loading={creating}
        onKeyUp={(keyUpEvent) => {
          if (keyUpEvent.key === 'Enter') onEnter(keyUpEvent.target.value);
        }}
        placeholder="Search phrase"
        ref={inputRef}
        size="big"
      />
      {watchDraft && (
        <WatchModal
          onClose={() => setWatchDraft(undefined)}
          onSave={createWatch}
          onSearchTextChange={(searchText) => {
            setWatchDraft({ searchText });

            // the field underneath is uncontrolled and read from a ref, so it
            // has to be written to directly
            const input = inputRef?.current?.inputRef?.current;

            if (input) {
              input.value = searchText;
            }
          }}
          open
          searchText={watchDraft.searchText}
        />
      )}
    </Segment>
  );
};

export default SearchBar;
