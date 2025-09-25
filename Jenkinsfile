// Jenkins Declarative Pipeline for POIneer.Render
// - Builds & tests on 'develop'
// - Placeholder "deploy" on 'release/*'
// - Avoids heavy rendering on Jenkins
// - Uses repository script to ensure dotnet SDK if missing

pipeline {
  agent any

  options {
    ansiColor('xterm')
    timestamps()
    disableConcurrentBuilds()
    buildDiscarder(logRotator(numToKeepStr: '20', artifactNumToKeepStr: '10'))
    timeout(time: 30, unit: 'MINUTES')
  }

  environment {
    // Speed up dotnet CLI
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    NUGET_PACKAGES = "${WORKSPACE}/.nuget/packages"
    // Desired SDK version for the project; adjust if you bump SDK
    DOTNET_SDK_VERSION = '9.0.305'
    // Project paths
    RENDER_CSProj = 'src/POIneer.Render/POIneer.Render.csproj'
    PUBLISH_DIR = 'out/POIneer.Render'
  }

  stages {

    stage('Checkout') {
      steps {
        checkout scm
        sh 'git --no-pager log -1 --pretty=fuller'
      }
    }

    stage('Ensure dotnet SDK') {
      steps {
        sh '''
          set -eux
          if ! command -v dotnet >/dev/null 2>&1; then
            echo "[setup] dotnet not found; trying repository install script..."
            if [ -x scripts/dotnet/install-dotnet-sdk.sh ]; then
              # Install into /opt/dotnet/<version> and symlink /usr/local/bin/dotnet (as in your setup)
              sudo scripts/dotnet/install-dotnet-sdk.sh "$DOTNET_SDK_VERSION"
            else
              echo "ERROR: scripts/dotnet/install-dotnet-sdk.sh not found or not executable."
              exit 2
            fi
          fi

          # Prefer your /usr/local/bin symlink if present
          export PATH="/usr/local/bin:$PATH"

          # Verify correct SDK available; if multiple SDKs exist, we'll still use global.json if present.
          dotnet --info
        '''
      }
    }

    stage('Restore') {
      steps {
        sh '''
          set -eux
          mkdir -p "${NUGET_PACKAGES}"
          dotnet restore "${RENDER_CSProj}" --nologo
        '''
      }
    }

    stage('Build') {
      steps {
        sh '''
          set -eux
          dotnet build "${RENDER_CSProj}" -c Release --no-restore --nologo
        '''
      }
    }

    stage('Test') {
      when {
        branch 'develop'
      }
      steps {
        sh '''
          set -eux
          # Discover and test all test projects if you have them under tests/
          if ls tests/**/**/*.csproj tests/**/*.csproj tests/*.csproj >/dev/null 2>&1; then
            for t in $(ls tests/**/**/*.csproj tests/**/*.csproj tests/*.csproj 2>/dev/null || true); do
              echo "Running tests for $t"
              # TRX is fine to archive; JUnit would require a logger package
              dotnet test "$t" -c Release --no-build --nologo --logger "trx;LogFileName=test-results.trx"
            done
          else
            echo "No test projects found (tests/*.csproj). Skipping."
          fi
        '''
      }
      post {
        always {
          // Archive any TRX files so you can review them
          archiveArtifacts artifacts: '**/TestResults/**/*.trx', fingerprint: true, onlyIfSuccessful: false
        }
      }
    }

    stage('Publish (App)') {
      steps {
        sh '''
          set -eux
          rm -rf "${PUBLISH_DIR}"
          dotnet publish "${RENDER_CSProj}" -c Release -o "${PUBLISH_DIR}" --no-build --nologo
          tar -C "${PUBLISH_DIR}" -czf "poineer-render_${BRANCH_NAME}.tar.gz" .
        '''
      }
      post {
        success {
          archiveArtifacts artifacts: 'poineer-render_*.tar.gz', fingerprint: true
        }
      }
    }

    stage('Deploy (placeholder)') {
      when {
        expression {
          // Run on branches like release/1.0.0, release/v0.3, etc.
          return env.BRANCH_NAME ==~ /release\/.+/
        }
      }
      steps {
        sh '''
          set -eux
          echo "[deploy] Release branch detected: ${BRANCH_NAME}"
          echo "[deploy] This is a placeholder. No heavy rendering and no live deploy performed on Jenkins."
          echo "[deploy] Here you could: rsync the published app to your render server, trigger a job, etc."
          # Example (commented):
          # rsync -avz --delete "${PUBLISH_DIR}/" user@render-host:/opt/poineer/render/
          # ssh user@render-host 'sudo systemctl restart poineer-render.service'
        '''
      }
    }

    stage('(Optional) Dry-Run Render Check') {
      when {
        allOf {
          branch 'develop'
          expression { return params?.RUN_RENDER_CHECK == true }
        }
      }
      steps {
        sh '''
          set -eux
          echo "[check] Running a short sanity-check invocation (no heavy work expected)."
          # Example of a quick, no-op style run if your app supports it:
          # dotnet "${PUBLISH_DIR}/POIneer.Render.dll" --help
        '''
      }
    }
  }

  parameters {
    // If you ever want to trigger a lightweight health check manually on develop
    booleanParam(name: 'RUN_RENDER_CHECK', defaultValue: false, description: 'Run a quick, no-op sanity check on develop (no heavy rendering).')
  }

  post {
    always {
      script {
        def emoji = currentBuild.currentResult == 'SUCCESS' ? '✅' : (currentBuild.currentResult == 'UNSTABLE' ? '⚠️' : '❌')
        echo "${emoji} Build result: ${currentBuild.currentResult}"
      }
    }
    failure {
      echo "Build failed. See logs above."
    }
  }
}
