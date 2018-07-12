# Public domain license.
# Author: igor.zavoychinskiy@gmail.com
# GitHub: https://github.com/ihsoft/KSPDev_ReleaseBuilder

"""A client library to communicate with Kerbal CurseForge via API.

Example:
  import CurseForgeClient

  CurseForgeClient.CURSE_PROJECT_ID = '123456'
  CurseForgeClient.CURSE_API_TOKEN = '11111111-2222-3333-4444-555555555555'
  print 'KSP 1.4.*:', CurseForgeClient.GetVersions(r'1\.4\.\d+')
  CurseForgeClient.UploadFile(
      '/var/files/archive.zip', '# BLAH!', r'1\.4\.\d+')
"""
import json
import re
import urllib2
import FormDataUtil


# The token to use when accessing CurseForge. NEVER commit it to GitHub!
# The caller code must set this variable before using the client.
# To get it, go tho the projects account on CurseForge:
# Account/Preferences/My API Tokens/Generate Token
CURSE_API_TOKEN = None

# The CurseForge project ID to work with. It must be set before using the client.
# To get it, simply open the project overview. It'll be in the
# "About This Project" section.
CURSE_PROJECT_ID = None

# This binds this client to the KSP namespace.
CURSE_BASE_URL = 'https://kerbal.curseforge.com'

# The actions paths.
CURSE_API_UPLOAD_URL_TMPL = '/api/projects/{project}/upload-file'
CURSE_API_GET_VERSIONS = '/api/game/versions'

# The cache for the known versions of the game. It's requested only once.
cached_versions = None


def GetKSPVersions(pattern=None):
  """Gets the available versions of the game.

  This method caches the versions, fetched from the server. It's OK to call it
  multiple times, it will only request the server once.

  Note, that the versions call requires an authorization token.
  See {@CURSE_API_TOKEN}.

  Args:
    pattern: A regexp string to apply on the result. If not provided, all the
        versions will be returned.
  Returns:
    A list of objects: { 'name': <KSP name>, 'id': <CurseForge ID> }. The list
    will be filtered if the pattern is set.
  """
  global cached_versions
  if not cached_versions:
    print 'Requesting versions from:', CURSE_BASE_URL
    url, headers = _GetAuthorizedEndpoint(CURSE_API_GET_VERSIONS);
    json_response = json.loads(_CallAPI(url, None, headers))
    cached_versions = map(lambda x: {'name': x['name'], 'id': x['id']}, json_response)
  if pattern:
    regex = re.compile(pattern)
    return filter(lambda x: regex.match(x['name']), cached_versions)
  return cached_versions


def UploadFileEx(metadata, filepath):
  """Uploads the file to the CurseForce project given the full metadata.

  Args:
    metadata: See https://authors.curseforge.com/docs/api for details.
    filepath: A full or relative path to the local file.
  Returns:
    The response object, returned by the API.
  """
  headers, data = FormDataUtil.EncodeFormData([
      { 'name': 'metadata', 'data': metadata },
      { 'name': 'file', 'filename': filepath },
  ])
  url, headers = _GetAuthorizedEndpoint(
      CURSE_API_UPLOAD_URL_TMPL.format(project=PROJECT_ID), headers)
  return json.loads(_CallAPI(url, data, headers))


def UploadFile(filepath, changelog, versions_pattern,
               title=None, release_type='release',
               changelog_type='markdown'):
  """Uploads the file to the CurseForge project.

  Args:
    filepath: A full or relative path to the local file.
    changelog: The change log content.
    versions_pattern: The RegExp string to find the target versions.
    title: The user friendly title of the file. If not provided, then the file
        name will be used.
    release_type: The type of the release. Allowed values: release, alpha, beta.
    changelog_type: The formatting type of the changelog. Allowed values:
        text, html, markdown.
  Returns:
    The response object, returned by the API.
  """
  metadata = {
    'changelog': changelog,
    'changelogType': changelog_type,
    'displayName': title,
    'gameVersions': map(lambda x: x['id'], GetKSPVersions(versions_pattern)),
    'releaseType': release_type,
  }
  return UploadFileEx(metadata, filepath)


def _CallAPI(url, data, headers):
  """Invokes the API call. Raises in case of any error."""
  try:
    request = urllib2.Request(url, data, headers)
    response = urllib2.urlopen(request)
  except urllib2.HTTPError as ex:
    error_message = ex.read()
    print 'API call failed:', error_message
    raise ex
  return response.read()


def _GetAuthorizedEndpoint(api_path, headers=None):
  """Gets API URL and the authorization headers.

  The authorization token must be set in the global variable CURSE_API_TOKEN.
  Otherwise, the endpoint will try to access the function anonymously. Many
  functions won't work in this mode.
  """
  url = CURSE_BASE_URL + api_path
  if not headers:
    headers = {}
  if CURSE_API_TOKEN:
    headers['X-Api-Token'] = CURSE_API_TOKEN
  return url, headers
