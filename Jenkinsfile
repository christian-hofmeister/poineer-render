// Jenkins Declarative Pipeline for POIneer.Render
// - Builds and tests on 'develop'
// - Creates a published application archive
// - Deploys verified Git release tags to the VPS
// - Avoids heavy rendering on Jenkins

pipeline {
  agent any

  options {
    ansiColor('xterm')
    timestamps()
    skipDefaultCheckout()
    disableConcurrentBuilds()
    buildDiscarder(logRotator(numToKeepStr: '20', artifactNumToKeepStr: '10'))
    timeout(time: 30, unit: 'MINUTES')
  }

  environment {
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    DOTNET_ENVIRONMENT = 'Production'

    NUGET_PACKAGES = "${WORKSPACE}/.nuget/packages"

    DOTNET_SDK_VERSION = '10.0.201'

    RENDER_CSPROJ = 'src/POIneer.Render/POIneer.Render.csproj'
    RENDER_PROJECT_DIR = 'src/POIneer.Render'
    PUBLISH_DIR = 'out/POIneer.Render'
    DOCKER_IMAGE = 'poineer-render'
    PLANETILER_VERSION = '0.10.2'

    COVERAGE_MIN = '25'

    // Jenkins runs directly on the VPS (issue #107) - deploying is a local sync into
    // this fixed directory layout, not a remote copy, so no SSH credential is needed.
    DEPLOY_ROOT = '/opt/poineer-render'
    DEPLOY_APP_DIR = '/opt/poineer-render/app'
    DOTNET_CURRENT = '/opt/dotnet/current/dotnet'
  }

  parameters {
    booleanParam(
      name: 'RUN_RENDER_CHECK',
      defaultValue: false,
      description: 'Run a quick sanity check on develop. This must not perform heavy rendering.'
    )
    string(
      name: 'PLANETILER_SHA256',
      defaultValue: '',
      description: 'SHA-256 of planetiler.jar for release tags. Defaults to CI_PLANETILER_SHA256 on the agent.'
    )
    string(
      name: 'FLYWAY_SHA256',
      defaultValue: '',
      description: 'SHA-256 of the Flyway Linux x64 tarball for release tags. Defaults to CI_FLYWAY_SHA256 on the agent.'
    )
  }

  stages {
    stage('Checkout') {
      steps {
        sh 'rm -f docker-image-tag.txt'
        checkout scm
        sh 'git --no-pager log -1 --pretty=fuller'
      }
    }

    stage('Validate Release Tag') {
      when {
        buildingTag()
      }
      steps {
        script {
          if (!(env.TAG_NAME ==~ /v[0-9]+\.[0-9]+\.[0-9]+/)) {
            error('Release tags must match vX.Y.Z.')
          }

          def releaseCommit = sh(script: 'git rev-parse HEAD', returnStdout: true).trim()
          // Use the SCM checkout credentials and full main history, even if the tag checkout is shallow.
          dir('.release-main-checkout') {
            deleteDir()
            try {
              checkout changelog: false, poll: false, scm: scmGit(
                branches: [[name: 'origin/main']],
                userRemoteConfigs: scm.userRemoteConfigs.collect { remote ->
                  [url: remote.url, credentialsId: remote.credentialsId ?: '',
                   refspec: '+refs/heads/main:refs/remotes/origin/main']
                },
                extensions: [cloneOption(honorRefspec: true, noTags: true)]
              )
              withEnv(["RELEASE_COMMIT=${releaseCommit}"]) {
                sh '''
                  if ! git merge-base --is-ancestor "$RELEASE_COMMIT" refs/remotes/origin/main; then
                    echo "Release tag ${TAG_NAME} must point to a commit reachable from main."
                    exit 1
                  fi
                '''
              }
            } finally {
              deleteDir()
            }
          }
          env.VERIFIED_RELEASE_TAG = env.TAG_NAME
        }
      }
    }

    stage('Debug dotnet') {
      steps {
        sh '''
          set -eux
          which -a dotnet || true
          echo "PATH=$PATH"
          echo "DOTNET_ROOT=${DOTNET_ROOT-}"
          dotnet --version || true
          dotnet --version
          dotnet --list-sdks
        '''
      }
    }

    stage('Restore') {
      steps {
        sh '''
          set -eux
          mkdir -p "${NUGET_PACKAGES}"
          dotnet restore "${RENDER_CSPROJ}" --nologo
        '''
      }
    }

    stage('Build') {
      steps {
        sh '''
          set -eux
          dotnet build "${RENDER_CSPROJ}" -c Release --no-restore --nologo
        '''
      }
    }

    stage('Test with Coverage') {
      steps {
        withEnv(['DOTNET_ENVIRONMENT=CI']) {
          sh '''
            set -eux

            echo "WORKSPACE=$WORKSPACE"
            pwd

            echo "Directory overview:"
            find . -maxdepth 3 -type d | sort

            echo "Checking for flyway and java:"
            which flyway || true
            flyway -v || true
            java -version || true

            echo "Running tests with coverage collection..."
            dotnet test POIneerRender.sln -c Release --nologo \
              --results-directory TestResults \
              --logger "junit;LogFilePath=TestResults/test-results.junit.xml" \
              /p:CollectCoverage=true \
              /p:CoverletOutputFormat=cobertura \
              /p:CoverletOutput=TestResults/Coverage/coverage

            echo "[diag] Coverage files:"
            find . -type f -name "coverage.cobertura.xml" -print

            echo "[diag] JUnit files:"
            find . -type f -name "test-results.junit.xml" -print

            echo "[diag] TRX files:"
            find . -type f -name "*.trx" -print
          '''
        }
      }
      post {
        always {
          junit '**/TestResults/**/test-results.junit.xml'

          recordCoverage(
            tools: [[parser: 'COBERTURA', pattern: '**/TestResults/**/coverage.cobertura.xml']],
            sourceCodeRetention: 'LAST_BUILD',
            failOnError: true
          )

          script {
            def min = env.COVERAGE_MIN ? env.COVERAGE_MIN.toInteger() : 25

            def pct = sh(
              script: '''
                set -eu
                f="$(ls -1 **/TestResults/**/coverage.cobertura.xml | head -n1)"
                awk -F'"' "/^<coverage/ {for(i=1;i<=NF;i++){if(\\$i ~ /line-rate=/){print \\$(i+1); exit}}}" "$f" \
                  | awk '{printf("%.0f\\n", $1*100)}'
              ''',
              returnStdout: true
            ).trim()

            echo "Line coverage detected: ${pct}% (threshold: ${min}%)"

            if (pct.isInteger() && pct.toInteger() < min) {
              currentBuild.result = 'FAILURE'
              echo "Coverage is below the configured threshold."
            } else {
              echo "Coverage threshold met."
            }
          }
        }
      }
    }

    stage('Coverage Report (HTML)') {
      when {
        expression { return fileExists('TestResults/Coverage/coverage.cobertura.xml') }
      }
      steps {
        sh '''
          set -eux
          dotnet tool update -g dotnet-reportgenerator-globaltool || dotnet tool install -g dotnet-reportgenerator-globaltool
          export PATH="$HOME/.dotnet/tools:$PATH"

          reportgenerator \
            -reports:**/TestResults/**/coverage.cobertura.xml \
            -targetdir:CoverageReport \
            -reporttypes:Html
        '''
      }
      post {
        always {
          archiveArtifacts artifacts: 'CoverageReport/**', fingerprint: false
        }
      }
    }

    stage('Publish App') {
      steps {
        sh '''
          set -eux
          rm -rf "${PUBLISH_DIR}"

          dotnet publish "${RENDER_CSPROJ}" -c Release -o "${PUBLISH_DIR}" --no-build --nologo

          SAFE_BRANCH="$(echo "$BRANCH_NAME" | tr '/ ' '__')"
          ARCHIVE="poineer-render_${SAFE_BRANCH}.tar.gz"

          tar -C "${PUBLISH_DIR}" -czf "$ARCHIVE" .
          echo "Created archive: $ARCHIVE"
        '''
      }
      post {
        success {
          archiveArtifacts artifacts: 'poineer-render_*.tar.gz', fingerprint: true
        }
      }
    }

    stage('Build Docker Image') {
      steps {
        sh '''
          set -eux

          SAFE_BRANCH="$(printf '%s' "$BRANCH_NAME" | sed 's/[^A-Za-z0-9_.-]/_/g; s/^[.-]/_/')"
          MAX_SAFE_BRANCH_LENGTH=$((128 - ${#BUILD_NUMBER} - 1))
          if [ "$MAX_SAFE_BRANCH_LENGTH" -ge 1 ]; then
            SAFE_BRANCH="$(printf '%s' "$SAFE_BRANCH" | cut -c "1-${MAX_SAFE_BRANCH_LENGTH}")"
          else
            SAFE_BRANCH=branch
          fi
          [ -n "$SAFE_BRANCH" ] || SAFE_BRANCH=branch
          IMAGE_TAG="${DOCKER_IMAGE}:${SAFE_BRANCH}-${BUILD_NUMBER}"
          echo "${IMAGE_TAG}" > docker-image-tag.txt

          PLANETILER_SHA256="${PLANETILER_SHA256:-${CI_PLANETILER_SHA256:-}}"
          FLYWAY_SHA256="${FLYWAY_SHA256:-${CI_FLYWAY_SHA256:-}}"
          if [ -n "${VERIFIED_RELEASE_TAG:-}" ]; then
            if [ -z "$PLANETILER_SHA256" ] || [ -z "$FLYWAY_SHA256" ]; then
              echo "Release tag builds require both Planetiler and Flyway SHA-256 checksums."
              exit 1
            fi
          fi

          docker build \
            --build-arg PLANETILER_VERSION="${PLANETILER_VERSION}" \
            --build-arg PLANETILER_SHA256="${PLANETILER_SHA256:-}" \
            --build-arg FLYWAY_SHA256="${FLYWAY_SHA256:-}" \
            --build-arg FLYWAY_SHA1="${FLYWAY_SHA1:-}" \
            -t "${IMAGE_TAG}" \
            .
        '''
      }
      post {
        success {
          archiveArtifacts artifacts: 'docker-image-tag.txt', fingerprint: false
        }
      }
    }

    stage('Verify Docker Image') {
      steps {
        sh '''
          set -eux

          IMAGE_TAG="$(cat docker-image-tag.txt)"

          docker run --rm "${IMAGE_TAG}" --Renderer:DryRun=true

          docker run --rm --entrypoint sh "${IMAGE_TAG}" -c \
            'id && java -version && flyway -v && osmium --version && test -s /opt/poineer-render/tools/planetiler/planetiler.jar && test -f /opt/poineer-render/app/migrations/flyway-poi.toml && test -d /opt/poineer-render/app/migrations/sql/poi'

          docker run --rm --entrypoint sh "${IMAGE_TAG}" -c \
            'cd /opt/poineer-render/app && flyway -configFiles="migrations/flyway-poi.toml" -url="jdbc:sqlite:/tmp/poineer-migration-smoke.sqlite" -locations="filesystem:migrations/sql/poi" migrate && test -s /tmp/poineer-migration-smoke.sqlite'
        '''
      }
    }

    // Non-release builds (feature/*, develop, etc.) have no further use for the
    // per-build CI image tag once Verify has passed - there is no Promote/Deploy
    // stage downstream for them. Remove it explicitly and visibly here (unlike the
    // silent, best-effort cleanup in the pipeline's post{always} block, which stays
    // in place as a safety net for builds that get aborted before reaching this
    // stage) so a failed removal actually shows up as a failed build instead of
    // disappearing into `|| true` / `/dev/null`.
    stage('Remove Non-Release Docker Image') {
      when {
        expression {
          return !env.VERIFIED_RELEASE_TAG
        }
      }
      steps {
        sh '''
          set -eux
          IMAGE_TAG="$(cat docker-image-tag.txt)"
          docker image rm -f "${IMAGE_TAG}"
        '''
      }
    }

    stage('Deploy to VPS') {
      // Jenkins runs directly on the VPS (confirmed: /opt/poineer-render is already
      // owned by the jenkins user) - so unlike the original placeholder sketch, this
      // is a local filesystem sync, not an SSH/rsync-to-a-remote-host step (issue #107).
      when {
        expression { return env.VERIFIED_RELEASE_TAG && currentBuild.currentResult == 'SUCCESS' }
      }
      steps {
        sh '''
          set -eux
          echo "[deploy] Deploying ${VERIFIED_RELEASE_TAG} to ${DEPLOY_APP_DIR} ..."

          mkdir -p "${DEPLOY_APP_DIR}" "${DEPLOY_ROOT}/logs" "${DEPLOY_ROOT}/scripts"

          # --delete removes anything left over from a previous deploy that this
          # release no longer produces (e.g. a renamed/removed file). Safe because
          # DEPLOY_APP_DIR only ever holds what "dotnet publish" produced - nothing
          # under it is ever hand-edited on the VPS.
          rsync -a --delete "${PUBLISH_DIR}/" "${DEPLOY_APP_DIR}/"

          echo "[deploy] Deployed contents:"
          ls -la "${DEPLOY_APP_DIR}"
        '''
      }
    }

    stage('Verify Deployment') {
      // Confirms the just-deployed artifact actually starts against the real
      // Production config (appsettings.Production.json + regions.production.json
      // path resolution) - --Renderer:DryRun=true makes Runner log its resolved
      // paths and exit 0 immediately, before touching the network, the lock file,
      // or any region, so this is safe to run unattended on the VPS (issue #107).
      when {
        expression { return env.VERIFIED_RELEASE_TAG && currentBuild.currentResult == 'SUCCESS' }
      }
      steps {
        sh '''
          set -eux
          echo "[verify] Starting the deployed artifact (dry run)..."

          "${DOTNET_CURRENT}" "${DEPLOY_APP_DIR}/POIneer.Render.dll" --Renderer:DryRun=true

          echo "[verify] Deployed renderer started successfully."
        '''
      }
    }

    stage('Promote Release Docker Image') {
      when {
        expression { return env.VERIFIED_RELEASE_TAG && currentBuild.currentResult == 'SUCCESS' }
      }
      steps {
        sh '''
          set -eux

          IMAGE_TAG="$(cat docker-image-tag.txt)"
          RELEASE_IMAGE_TAG="${DOCKER_IMAGE}:${VERIFIED_RELEASE_TAG}"
          PRODUCTION_IMAGE_TAG="${DOCKER_IMAGE}:production"

          docker tag "${IMAGE_TAG}" "${RELEASE_IMAGE_TAG}"
          docker tag "${IMAGE_TAG}" "${PRODUCTION_IMAGE_TAG}"

          {
            echo "${RELEASE_IMAGE_TAG}"
            echo "${PRODUCTION_IMAGE_TAG}"
          } > docker-promoted-tags.txt

          echo "[docker] Promoted ${IMAGE_TAG} to ${RELEASE_IMAGE_TAG} and ${PRODUCTION_IMAGE_TAG}."
          docker image ls "${DOCKER_IMAGE}"
        '''
      }
      post {
        success {
          archiveArtifacts artifacts: 'docker-promoted-tags.txt', fingerprint: false
        }
      }
    }

    stage('Prune Old Release Docker Images') {
      when {
        expression { return env.VERIFIED_RELEASE_TAG && currentBuild.currentResult == 'SUCCESS' }
      }
      steps {
        sh '''
          set -eux

          # Keep the two newest semantic release tags. The stable "production" tag is
          # never selected here because it does not match the version tag pattern.
          docker image ls "${DOCKER_IMAGE}" --format '{{.Repository}}:{{.Tag}}' \
            | grep -E "^${DOCKER_IMAGE}:v[0-9]+\\.[0-9]+\\.[0-9]+([.-][A-Za-z0-9_.-]+)?$" \
            | sort -V \
            | awk 'NR>2{print p2} {p2=p1; p1=$0}' \
            | while IFS= read -r image_tag; do docker image rm "$image_tag" || true; done
        '''
      }
    }

    stage('Optional Dry-Run Render Check') {
      when {
        allOf {
          branch 'develop'
          expression { return params.RUN_RENDER_CHECK == true }
        }
      }
      steps {
        dir("${RENDER_PROJECT_DIR}") {
          withEnv(['DOTNET_ENVIRONMENT=Development']) {
            sh '''
              set -eux
              echo "[check] Running POIneer.Render dry-run check from project directory."
              pwd
              dotnet run -c Release --no-build -- --Renderer:DryRun=true
            '''
          }
        }
      }
    }
  }

  post {
    always {
      script {
        def emoji = currentBuild.currentResult == 'SUCCESS' ? '✅' : (currentBuild.currentResult == 'UNSTABLE' ? '⚠️' : '❌')
        echo "${emoji} Build result: ${currentBuild.currentResult}"
      }
      sh '''
        set +e
        if [ -f docker-image-tag.txt ]; then
          IMAGE_TAG="$(cat docker-image-tag.txt)"
          echo "[docker] Removing CI image ${IMAGE_TAG} ..."
          docker image rm -f "${IMAGE_TAG}" >/dev/null 2>&1 || true
        fi
      '''
    }
    failure {
      echo "Build failed. See logs above."
    }
  }
}

